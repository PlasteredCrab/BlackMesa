using BlackMesa.Patches;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace BlackMesa.Components;

public class Barnacle : NetworkBehaviour, IHittable
{
    enum State
    {
        Extending,
        Idle,
        Pulling,
        Eating,
        Flinching,
        Dead,
    }

    private static readonly System.Random animationRandomizer = new();

    private static readonly HashSet<Barnacle> barnacles = [];

    public Animator animator;
    public BarnacleSounds sounds;

    [Header("Tongue")]
    public Rigidbody[] tongueSegments;

    [Header("Tongue Raycast")]
    public Transform raycastOrigin;
    public LayerMask tongueCastMask;
    public float maxLength;
    public float tongueGroundDistance;

    [Header("Tongue Speed")]
    public float defaultTongueRetractSpeed;
    public float defaultTongueDropSpeed;

    [Header("Grabbing")]
    public Transform dummyObject;
    public Transform holder;
    public Vector3 playerCenterOfMass;
    public float playerAngularDrag;
    public float holderPositionScale;

    [Header("Pulling")]
    public float eatDistance;

    [Header("Eating/Dropping")]
    public Transform itemStash;
    public Transform mouthAttachment;
    public float playerEatTime;
    public float itemEatTime;
    public VisualEffect pukeEffect;

    public AnimationCurve idleSoundTimeCurve;

    private const float gravity = 15.9f;

    private float tongueRetractSpeed;
    private float tongueDropSpeed;

    private Transform tongueParentTransform;
    private Vector3 retractedTongueLocalPosition;

    private State state = State.Idle;
    private float eatingTimeLeft = 0;
    private float flinchTimeLeft = 0;

    private CapsuleCollider[] tongueSegmentColliders;
    private float[] tongueSegmentMouthOffsets;

    private float targetTongueOffset = 0;
    private float currentTongueOffset = 0;
    private float eatingTongueOffset = 0;
    private int firstEnabledSegment = 0;

    private Transform dummyObjectParent;
    private Rigidbody dummyObjectBody;
    private Joint dummyObjectJoint;
    private float defaultAngularDrag;

    private GrabbableObject grabbedItem;
    private PlayerControllerB grabbedPlayer;
    private DeadBodyInfo grabbedBody;
    private Transform grabbedPlayerPreviousParent;
    private bool centeringHolderPosition = false;

    private float idleSoundTime = -1;
    private float pukeTravelTime = 2;

    private readonly List<GrabbableObject> eatenItems = [];

    private void Awake()
    {
        var tongueStart = tongueSegments[0];
        tongueParentTransform = tongueStart.transform.parent;
        retractedTongueLocalPosition = tongueStart.transform.localPosition;

        tongueRetractSpeed = defaultTongueRetractSpeed;
        tongueDropSpeed = defaultTongueDropSpeed;

        tongueSegmentColliders = new CapsuleCollider[tongueSegments.Length];
        tongueSegmentMouthOffsets = new float[tongueSegments.Length];

        var currentOffset = 0f;
        for (var i = tongueSegments.Length; i-- > 0;)
        {
            tongueSegmentColliders[i] = tongueSegments[i].GetComponent<CapsuleCollider>();
            currentOffset += tongueSegments[i].transform.TransformVector(new Vector3(0, 0, tongueSegmentColliders[i].height)).magnitude;
            tongueSegmentMouthOffsets[i] = currentOffset;
        }

        dummyObjectParent = dummyObject.parent;
        dummyObjectBody = dummyObject.GetComponent<Rigidbody>();
        dummyObjectJoint = dummyObjectBody.GetComponent<Joint>();
        defaultAngularDrag = dummyObjectBody.angularDrag;

        tongueParentTransform.gameObject.SetActive(false);
    }

    private void Start()
    {
        animator.SetTrigger("Extend");
        DropTongue();

        barnacles.Add(this);

        tongueParentTransform.gameObject.SetActive(true);
        for (var i = 1; i < tongueSegments.Length; i++)
        {
            tongueSegments[i].isKinematic = false;
            var gravityComponent = tongueSegments[i].GetComponent<RigidbodyGravity>();
            if (gravityComponent != null)
                gravityComponent.gravity = Vector3.down * gravity;
        }

        pukeEffect.SetFloat("Gravity", gravity);
    }

    private void SetIdleSoundTimer()
    {
        if (!IsOwner)
            return;

        idleSoundTime = idleSoundTimeCurve.Evaluate((float)animationRandomizer.NextDouble());
    }

    private void DisableHolderPhysics()
    {
        dummyObjectBody.isKinematic = true;
        dummyObjectBody.centerOfMass = Vector3.zero;
        dummyObjectBody.angularDrag = defaultAngularDrag;
        dummyObjectBody.interpolation = RigidbodyInterpolation.None;
    }

    private void EnableHolderPhysics()
    {
        dummyObjectBody.isKinematic = false;
        dummyObjectBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void SetState(State newState)
    {
        if (newState == state)
            return;

        state = newState;
    }

    private void DropTongue()
    {
        dummyObject.SetParent(dummyObjectParent, false);
        dummyObject.position = transform.position;
        dummyObjectJoint.connectedBody = null;
        DisableHolderPhysics();

        var distance = maxLength;
        if (Physics.SphereCast(raycastOrigin.position, 0.1f, Vector3.down, out var hit, maxLength, tongueCastMask, QueryTriggerInteraction.Ignore))
            distance = hit.distance;
        targetTongueOffset = distance - tongueGroundDistance;

        sounds.groundAudioSource.transform.position = hit.point;
        pukeTravelTime = Mathf.Sqrt(2 * Vector3.Distance(pukeEffect.transform.position, hit.point) / gravity);

        SetState(State.Extending);
    }

    internal bool HasGrabbedObject => grabbedItem != null || grabbedPlayer != null || grabbedBody != null;

    internal bool CanGrab => !HasGrabbedObject && (state == State.Extending || state == State.Idle);

    internal bool IsDead => state == State.Dead;

    public void TryGrabPlayerOrHeldItem(PlayerControllerB player)
    {
        if (!CanGrab)
            return;
        if (!player.IsOwner)
            return;

        var heldItem = player.currentlyHeldObjectServer;
        if (heldItem != null && heldItem.IsOwner)
        {
            // Only grab the item if the player is within ~63 degrees of facing the tongue.
            var cameraTransform = player.gameplayCamera.transform;
            Transform closestSegment = tongueSegments[0].transform;
            var closestSegmentDistanceSqr = float.PositiveInfinity;
            foreach (var currentSegment in tongueSegments)
            {
                var currentSegmentDistanceSqr = (cameraTransform.position - currentSegment.transform.position).sqrMagnitude;
                if (currentSegmentDistanceSqr < closestSegmentDistanceSqr)
                {
                    closestSegment = currentSegment.transform;
                    closestSegmentDistanceSqr = currentSegmentDistanceSqr;
                }
            }
            var closestSegmentDirection = (closestSegment.position - cameraTransform.position).normalized;
            var dot = Vector3.Dot(cameraTransform.forward, closestSegmentDirection);
            if (dot >= 0.45f)
            {
                GrabItemServerRpc(heldItem);
                return;
            }
        }

        GrabPlayerServerRpc(player);
    }

    private void GetAttachmentPosition(Vector3 rootPosition, Vector3 hitPosition, Collider collider, out Rigidbody attachSegment, out Vector3 attachPosition, out Vector3 holderPosition, out float tongueOffset)
    {
        attachSegment = tongueSegments[^1];
        tongueOffset = tongueSegmentMouthOffsets[^1];
        attachPosition = hitPosition;
        holderPosition = Vector3.zero;

        var closestDistanceSqr = float.PositiveInfinity;

        for (var i = 0; i < tongueSegments.Length; i++)
        {
            var segmentCollider = tongueSegmentColliders[i];
            var segmentTransform = segmentCollider.transform;
            var segmentCenter = segmentTransform.TransformPoint(segmentCollider.center);

            var distanceSqr = (hitPosition - segmentCenter).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                var segmentUp = segmentTransform.right;
                closestDistanceSqr = distanceSqr;
                attachSegment = tongueSegments[i];
                attachPosition = segmentCollider.ClosestPoint(hitPosition);
                tongueOffset = tongueSegmentMouthOffsets[i] + Vector3.Dot(attachPosition - segmentTransform.position, segmentUp);
            }
        }

        if (collider != null)
        {
            var closestPointOnTarget = collider.ClosestPoint(attachPosition);
            holderPosition = attachPosition + Vector3.Scale(rootPosition - closestPointOnTarget, Vector3.one * holderPositionScale);
        }
    }

    private void DisableAllPhysics()
    {
        DisableHolderPhysics();
        foreach (var tongueSegment in tongueSegments)
            tongueSegment.isKinematic = true;
    }

    private Vector3 GetColliderCenter(Collider collider)
    {
        if (collider is BoxCollider box)
            return box.center;
        if (collider is SphereCollider sphere)
            return sphere.center;
        if (collider is CapsuleCollider capsule)
            return capsule.center;
        return Vector3.zero;
    }

    private static void SetCenterOfMassToWorldPosition(Rigidbody rigidBody, Vector3 position)
    {
        var transform = rigidBody.transform;
        var localPosition = transform.InverseTransformPoint(position);
        rigidBody.centerOfMass = localPosition;
    }

    private static void RemoveItemFromPlayerInventory(PlayerControllerB player, int itemSlot)
    {
        var item = player.ItemSlots[itemSlot];
        if (item == null)
            return;

        player.SetSpecialGrabAnimationBool(setTrue: false, item);
        player.playerBodyAnimator.SetBool("cancelHolding", true);

        HUDManager.Instance.itemSlotIcons[itemSlot].enabled = false;
        HUDManager.Instance.holdingTwoHandedItem.enabled = false;

        player.SetObjectAsNoLongerHeld(player.isInElevator, player.isInHangarShipRoom, Vector3.zero, item);
        item.DiscardItemOnClient();
        player.currentlyHeldObjectServer = null;
    }

    internal void GrabItem(GrabbableObject item)
    {
        if (!item.IsOwner)
            return;
        GrabItemServerRpc(item);
    }

    [ServerRpc(RequireOwnership = false)]
    private void GrabItemServerRpc(NetworkBehaviourReference itemReference)
    {
        GrabItemClientRpc(itemReference);
    }

    [ClientRpc]
    private void GrabItemClientRpc(NetworkBehaviourReference itemReference)
    {
        if (!itemReference.TryGet(out GrabbableObject item))
        {
            BlackMesaInterior.Logger.LogError($"{nameof(GrabItemClientRpc)} called with invalid item reference.");
            return;
        }
        GrabItemOnClient(item);
    }

    private void GrabItemOnClient(GrabbableObject item)
    {
        if (!CanGrab)
            return;

        if (item.playerHeldBy is { } player)
            RemoveItemFromPlayerInventory(player, player.currentItemSlot);

        var itemParent = item.parentObject ?? item.transform;
        var itemCollider = item.GetComponent<Collider>();
        GetAttachmentPosition(itemParent.position, item.transform.position, itemCollider, out var segment, out var attachPosition, out var holderPosition, out eatingTongueOffset);
        eatingTongueOffset += eatDistance;

        dummyObject.SetPositionAndRotation(attachPosition, itemParent.rotation);
        dummyObjectJoint.connectedBody = segment;
        SetCenterOfMassToWorldPosition(dummyObjectBody, item.transform.TransformPoint(GetColliderCenter(itemCollider)));
        dummyObjectBody.angularDrag = defaultAngularDrag;
        holder.position = holderPosition;
        holder.localRotation = Quaternion.identity;
        EnableHolderPhysics();

        item.parentObject = holder;
        grabbedItem = item;
        item.EnablePhysics(false);

        sounds.PlayGrabItemSound();

        BeginPulling();
    }

    [ServerRpc(RequireOwnership = false)]
    private void GrabPlayerServerRpc(NetworkBehaviourReference playerReference)
    {
        GrabPlayerClientRpc(playerReference);
    }

    [ClientRpc]
    private void GrabPlayerClientRpc(NetworkBehaviourReference playerReference)
    {
        if (!playerReference.TryGet(out PlayerControllerB player))
        {
            BlackMesaInterior.Logger.LogError($"{nameof(GrabPlayerClientRpc)} called with invalid player reference.");
            return;
        }
        GrabPlayer(player);
    }

    public void GrabPlayer(PlayerControllerB player)
    {
        if (!CanGrab)
            return;

        GetAttachmentPosition(player.transform.position, player.transform.position + (Vector3.up * 2.5f), player.playerCollider, out var segment, out var attachPosition, out var holderPosition, out eatingTongueOffset);
        eatingTongueOffset += eatDistance;

        dummyObject.SetPositionAndRotation(attachPosition, Quaternion.identity);
        dummyObjectJoint.connectedBody = segment;
        dummyObjectBody.centerOfMass = playerCenterOfMass;
        dummyObjectBody.angularDrag = playerAngularDrag;
        holder.position = holderPosition;
        holder.localRotation = Quaternion.identity;
        EnableHolderPhysics();

        grabbedPlayerPreviousParent = player.transform.parent;
        grabbedPlayer = player;
        grabbedPlayer.transform.SetParent(holder, false);
        PatchPlayerControllerB.SetPlayerPositionLocked(player, true);

        sounds.PlayGrabPlayerSound();

        BeginPulling();
    }

    public void BeginPulling()
    {
        sounds.PlayContactSound();
        StartCoroutine(BeginPullingCoroutine());
    }

    private IEnumerator BeginPullingCoroutine()
    {
        yield return new WaitForSeconds(0.2f);

        animator.SetTrigger("Pull");
        SetState(State.Pulling);
        centeringHolderPosition = false;
    }

    public void YankTongue(float distance)
    {
        if (IsDead)
            return;

        targetTongueOffset = currentTongueOffset - distance;
    }

    private void Update()
    {
        if (centeringHolderPosition)
        {
            var holderDistance = Vector3.Dot(holder.position - raycastOrigin.position, raycastOrigin.up);
            var centerPoint = raycastOrigin.position + raycastOrigin.up * holderDistance;

            holder.position = Vector3.Lerp(holder.position, centerPoint, 6 * Time.deltaTime);
        }

        if (state == State.Eating)
        {
            eatingTimeLeft -= Time.deltaTime;

            if (eatingTimeLeft <= 0)
            {
                animator.SetTrigger("Finish Eating");
                DropTongue();
            }
        }

        if (state == State.Flinching)
        {
            animator.SetInteger("Flinch", -1);

            flinchTimeLeft -= Time.deltaTime;

            if (flinchTimeLeft <= 0)
            {
                animator.SetTrigger("Extend");
                DropTongue();
            }
        }

        if (!IsOwner)
            return;

        if (state == State.Idle)
        {
            idleSoundTime -= Time.deltaTime;
            if (idleSoundTime <= 0)
            {
                sounds.PlayIdleSoundServerRpc();
                SetIdleSoundTimer();
            }
        }
    }

    private int GetFirstSegmentBelowMouth()
    {
        var targetOffset = currentTongueOffset + 1f;
        for (var i = 0; i < tongueSegments.Length; i++)
        {
            if (tongueSegmentMouthOffsets[i] < targetOffset)
                return i;
        }

        return tongueSegments.Length;
    }

    private void UpdateDisabledSegments()
    {
        var newFirstEnabledSegment = GetFirstSegmentBelowMouth();

        if (newFirstEnabledSegment < firstEnabledSegment)
        {
            while (firstEnabledSegment-- > newFirstEnabledSegment)
                tongueSegments[firstEnabledSegment].detectCollisions = true;
            firstEnabledSegment++;
        }
        else if (firstEnabledSegment < newFirstEnabledSegment)
        {
            while (firstEnabledSegment < newFirstEnabledSegment)
            {
                tongueSegments[firstEnabledSegment].detectCollisions = false;
                firstEnabledSegment++;
            }
        }
    }

    private void FixedUpdate()
    {
        if (state == State.Extending && targetTongueOffset == currentTongueOffset)
        {
            EnterIdleState();
            return;
        }

        if (currentTongueOffset < targetTongueOffset)
        {
            currentTongueOffset += tongueDropSpeed * Time.fixedDeltaTime;
            currentTongueOffset = Math.Min(currentTongueOffset, targetTongueOffset);
        }
        else
        {
            currentTongueOffset -= tongueRetractSpeed * Time.fixedDeltaTime;
            currentTongueOffset = Math.Max(currentTongueOffset, targetTongueOffset);
        }

        if (state == State.Pulling && HasGrabbedObject && currentTongueOffset < eatingTongueOffset)
            BeginEatingGrabbedObject();

        var newPosition = tongueParentTransform.TransformPoint(retractedTongueLocalPosition) + Vector3.down * currentTongueOffset;
        tongueSegments[0].MovePosition(newPosition);

        UpdateDisabledSegments();
    }

    private void EnterIdleState()
    {
        if (state != State.Extending)
            return;

        animator.SetTrigger("Idle");
        SetState(State.Idle);
    }

    public void BeginEatingGrabbedObject()
    {
        if (!IsOwner)
            return;
        BeginEatingGrabbedItemClientRpc();
    }

    [ClientRpc]
    private void BeginEatingGrabbedItemClientRpc()
    {
        if (state != State.Pulling)
            return;

        if (grabbedPlayer != null)
        {
            animator.SetTrigger("Bite Player");
            eatingTimeLeft = playerEatTime;
        }
        else if (grabbedItem != null)
        {
            animator.SetTrigger("Eat Item");
            eatingTimeLeft = itemEatTime;
        }
        else
        {
            return;
        }

        targetTongueOffset = eatingTongueOffset;
        SetState(State.Eating);
    }

    public void AttachHolderToMouth()
    {
        DisableHolderPhysics();
        dummyObjectJoint.connectedBody = null;
        dummyObject.SetParent(mouthAttachment, true);
        dummyObject.localPosition = Vector3.zero;
        centeringHolderPosition = true;
    }

    public void SwallowGrabbedItem()
    {
        if (!IsOwner)
            return;
        if (IsDead)
            return;
        SwallowGrabbedItemClientRpc();
    }

    [ClientRpc]
    private void SwallowGrabbedItemClientRpc()
    {
        grabbedItem.parentObject = itemStash;
        grabbedItem.EnableItemMeshes(false);
        eatenItems.Add(grabbedItem);
        grabbedItem = null;
    }

    public void BiteGrabbedPlayer(int damage)
    {
        if (!grabbedPlayer.IsOwner)
            return;
        if (IsDead)
            return;

        if (grabbedPlayer.criticallyInjured || !grabbedPlayer.AllowPlayerDeath())
        {
            SwallowGrabbedPlayerServerRpc();
            return;
        }

        grabbedPlayer.DamagePlayer(damage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Crushing);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SwallowGrabbedPlayerServerRpc()
    {
        SwallowGrabbedPlayerClientRpc();
    }

    [ClientRpc]
    private void SwallowGrabbedPlayerClientRpc()
    {
        animator.SetTrigger("Swallow Player");
    }

    public void KillPlayer()
    {
        if (!grabbedPlayer.IsOwner)
            return;
        KillPlayerServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void KillPlayerServerRpc()
    {
        if (grabbedPlayer.isPlayerDead || !grabbedPlayer.AllowPlayerDeath())
        {
            DropPlayerWithoutKillingClientRpc();
            return;
        }

        KillPlayerClientRpc();
    }

    [ClientRpc]
    private void KillPlayerClientRpc()
    {
        grabbedPlayer.KillPlayer(Vector3.zero, spawnBody: true, CauseOfDeath.Crushing, deathAnimation: 0);
    }

    [ClientRpc]
    private void DropPlayerWithoutKillingClientRpc()
    {
        DropPlayer();
        grabbedPlayer = null;
    }

    public static void OnRagdollSpawnedForPlayer(PlayerControllerB player)
    {
        foreach (var barnacle in barnacles)
        {
            if (barnacle.grabbedPlayer == player)
                barnacle.AttachRagdoll();
        }
    }

    private void AttachRagdoll()
    {
        const string methodName = $"{nameof(Barnacle)}.{nameof(AttachRagdoll)}()";

        if (grabbedPlayer.deadBody == null)
        {
            BlackMesaInterior.Logger.LogError($"{methodName}: Grabbed player {grabbedPlayer} has no ragdoll.");
            return;
        }

        var head = grabbedPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003/spine.004");

        if (head == null)
        {
            BlackMesaInterior.Logger.LogError($"{methodName}: Failed to find the head of {grabbedPlayer}'s ragdoll.");
            return;
        }
        if (!head.TryGetComponent<Rigidbody>(out var headRigidbody))
        {
            BlackMesaInterior.Logger.LogError($"{methodName}: {grabbedPlayer}'s ragdoll's head has no rigidbody.");
            return;
        }

        // Prevent collision from yeeting the body all over.
        foreach (var rigidBody in grabbedPlayer.deadBody.bodyParts)
            rigidBody.detectCollisions = false;

        DropPlayer();
        dummyObjectJoint.connectedBody = headRigidbody;
        grabbedBody = grabbedPlayer.deadBody;
        grabbedPlayer = null;
    }

    public void DeactivateRagdoll()
    {
        if (grabbedBody == null)
            return;

        grabbedBody.DeactivateBody(setActive: false);
        if (grabbedBody.grabBodyObject is { } bodyItem && bodyItem.playerHeldBy is { } player)
        {
            if (player.currentlyHeldObjectServer == bodyItem)
                player.DiscardHeldObject();

            for (var i = 0; i < player.ItemSlots.Length; i++)
            {
                if (player.ItemSlots[i] == bodyItem)
                    player.ItemSlots[i] = null;
            }

            bodyItem.EnablePhysics(false);
        }

        dummyObjectJoint.connectedBody = null;
        grabbedBody = null;
    }

    public void SpitPlayerGuts()
    {
        sounds.PlayPukeSound();
        PlayPukeEffect();
    }

    private void DropItem(GrabbableObject item)
    {
        var initialPosition = item.parentObject.position;
        if (item.transform.parent != null)
            initialPosition = item.transform.parent.InverseTransformPoint(initialPosition);

        item.startFallingPosition = initialPosition;
        item.parentObject = null;
        item.EnableItemMeshes(true);
        item.EnablePhysics(true);
        item.FallToGround(true);

        ResetHolder();
    }

    private void DropPlayer()
    {
        if (grabbedPlayer == null)
            return;

        PatchPlayerControllerB.SetPlayerPositionLocked(grabbedPlayer, false);
        var position = grabbedPlayer.transform.position;
        grabbedPlayer.transform.SetParent(grabbedPlayerPreviousParent, false);
        grabbedPlayer.transform.position = position;

        ResetHolder();
    }

    private void ResetHolder()
    {
        DisableHolderPhysics();
        dummyObjectJoint.connectedBody = null;
        dummyObject.position = transform.position;
        dummyObject.rotation = Quaternion.identity;
    }

    public void Stun(float duration)
    {
        StunServerRpc(duration);
    }

    [ServerRpc(RequireOwnership = false)]
    private void StunServerRpc(float duration)
    {
        StunClientRpc(duration);
    }

    [ClientRpc]
    private void StunClientRpc(float duration)
    {
        SetState(State.Flinching);
        flinchTimeLeft = duration;

        animator.SetInteger("Flinch", animationRandomizer.Next(2));
        animator.ResetTrigger("Bite Player");
        animator.ResetTrigger("Swallow Player");
        animator.ResetTrigger("Eat Item");
        animator.ResetTrigger("Finish Eating");

        sounds.PlayFlinchSound();

        if (grabbedItem != null)
        {
            DropItem(grabbedItem);
            grabbedItem = null;
        }

        DropPlayer();
        grabbedPlayer = null;
    }

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
    {
        if (IsDead)
            return false;
        if (force <= 0)
            return false;
        StartDyingServerRpc();
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartDyingServerRpc()
    {
        StartDyingClientRpc();
    }

    [ClientRpc]
    private void StartDyingClientRpc()
    {
        StartCoroutine(Die());
    }

    public void PlayPukeEffect()
    {
        pukeEffect.Reinit();
        pukeEffect.Play();
        StartCoroutine(PlayPukeSoundsCoroutine());
    }

    private IEnumerator PlayPukeSoundsCoroutine()
    {
        yield return new WaitForSeconds(pukeTravelTime);
        sounds.PlaySplashSound();
    }

    private IEnumerator Die()
    {
        sounds.PlayFlinchSound();

        SetState(State.Dead);
        animator.SetInteger("Death", animationRandomizer.Next(2));

        yield return new WaitForSeconds(1);

        if (grabbedItem != null)
        {
            DropItem(grabbedItem);
            grabbedItem = null;
        }

        DropPlayer();
        grabbedPlayer = null;

        sounds.PlayDeathSound();

        yield return new WaitForSeconds(0.33f);

        PlayPukeEffect();

        while (eatenItems.Count > 0)
        {
            yield return new WaitForSeconds(0.1f);
            var index = eatenItems.Count - 1;
            if (eatenItems[index] != null)
                DropItem(eatenItems[index]);
            eatenItems.RemoveAt(index);
        }
    }

    public override void OnDestroy()
    {
        if (grabbedItem != null)
        {
            DropItem(grabbedItem);
            grabbedItem = null;
        }

        DropPlayer();
        grabbedPlayer = null;

        foreach (var eatenItem in eatenItems)
        {
            if (eatenItem == null)
                continue;
            DropItem(eatenItem);
        }
        eatenItems.Clear();

        barnacles.Remove(this);
    }
}
