using BlackMesa.Patches;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlackMesa.Components;

public class Barnacle : MonoBehaviour, IHittable
{
    enum State
    {
        Extending,
        Idle,
        Pulling,
        Eating,
        Dead,
    }

    private static readonly System.Random animationRandomizer = new();

    private static readonly HashSet<Barnacle> barnacles = [];

    public Animator animator;

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

    private float tongueRetractSpeed;
    private float tongueDropSpeed;

    private Transform tongueParentTransform;
    private Vector3 retractedTongueLocalPosition;

    private State state = State.Idle;

    private CapsuleCollider[] tongueSegmentColliders;
    public float[] tongueSegmentMouthOffsets;

    public float targetTongueOffset = 0;
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

    private readonly List<GrabbableObject> eatenItems = [];

    private void Start()
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

        animator.SetTrigger("Extend");
        DropTongue();

        barnacles.Add(this);
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
        if (Physics.SphereCast(raycastOrigin.position, 0.1f, Vector3.down, out var hit, maxLength, tongueCastMask))
            distance = hit.distance;
        targetTongueOffset = distance - tongueGroundDistance;

        SetState(State.Extending);
    }

    internal bool HasGrabbedObject => grabbedItem != null || grabbedPlayer != null || grabbedBody != null;

    internal bool CanGrab => !HasGrabbedObject && (state == State.Extending || state == State.Idle);

    internal bool IsDead => state == State.Dead;

    public void TryGrabPlayerOrHeldItem(PlayerControllerB player)
    {
        if (!CanGrab)
            return;
        if (player != GameNetworkManager.Instance.localPlayerController)
            return;

        var heldItem = player.currentlyHeldObjectServer;
        if (heldItem != null)
        {
            GrabItem(heldItem);
        }
        else
        {
            GrabPlayer(player);
        }
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
    }

    public void GrabItem(GrabbableObject item)
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

        BeginPulling();
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

        BeginPulling();
    }

    public void BeginPulling()
    {
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
        if (state != State.Pulling)
            return;

        if (grabbedPlayer != null)
            animator.SetTrigger("Bite Player");
        else if (grabbedItem != null)
            animator.SetTrigger("Eat Item");
        else
            return;

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
        if (IsDead)
            return;

        grabbedItem.parentObject = itemStash;
        grabbedItem.EnableItemMeshes(false);
        eatenItems.Add(grabbedItem);
        grabbedItem = null;
        DropTongue();
    }

    public void BiteGrabbedPlayer(int damage)
    {
        if (IsDead)
            return;

        if (grabbedPlayer.criticallyInjured)
        {
            animator.SetTrigger("Swallow Player");
            return;
        }

        grabbedPlayer.DamagePlayer(damage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Crushing);
    }

    public void KillPlayer()
    {
        DropPlayer();
        grabbedPlayer.KillPlayer(Vector3.zero, spawnBody: true, CauseOfDeath.Crushing, deathAnimation: 0);
        // Check if the player was actually killed
        // AttachRagdoll() may be called inside KillPlayer() if a dead body spawns, so we need to check
        // for a null player here.
        if (grabbedPlayer?.deadBody == null)
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
        DropTongue();
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

    bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
    {
        if (IsDead)
            return false;
        if (force <= 0)
            return false;
        StartCoroutine(Die());
        return true;
    }

    private IEnumerator Die()
    {
        SetState(State.Dead);
        animator.SetInteger("Death", animationRandomizer.Next(2));

        if (grabbedItem != null)
        {
            DropItem(grabbedItem);
            grabbedItem = null;
        }

        DropPlayer();
        grabbedPlayer = null;

        yield return new WaitForSeconds(1.33f);

        while (eatenItems.Count > 0)
        {
            yield return new WaitForSeconds(0.1f);
            var index = eatenItems.Count - 1;
            if (eatenItems[index] != null)
                DropItem(eatenItems[index]);
            eatenItems.RemoveAt(index);
        }
    }

    private void OnDestroy()
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
