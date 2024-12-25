using BlackMesa.Interfaces;
using BlackMesa.Patches;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace BlackMesa.Components;

public class Barnacle : NetworkBehaviour, IDumbEnemy
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
    public float holderPositionScale;

    [Header("Pulling")]
    public float eatDistance;

    [Header("Eating/Dropping")]
    public Transform itemStash;
    public Transform mouthAttachment;
    public VisualEffect pukeEffect;

    public AnimationCurve idleSoundTimeCurve;

    private const float gravity = 15.9f;

    private float tongueRetractSpeed;
    private float tongueDropSpeed;

    private Transform tongueParentTransform;
    private Vector3 retractedTongueLocalPosition;

    private State state = State.Idle;
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
    private Joint holderJoint;
    private float defaultAngularDrag;

    private GrabbableObject grabbedItem;
    private PlayerControllerB grabbedPlayer;
    private Transform grabbedPlayerPreviousParent;
    private DeadBodyInfo grabbedBody;
    private EnemyAI grabbedEnemy;
    private bool centeringHolderPosition = false;
    private Vector3 centeredHolderLocalPosition;
    private bool centeringDummyObjectRotation = false;

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
        holderJoint = holder.GetComponent<Joint>();
        defaultAngularDrag = dummyObjectBody.angularDrag;

        tongueParentTransform.gameObject.SetActive(false);
    }

    private void Start()
    {
        animator.CrossFadeInFixedTime("Extending", 0.25f);
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

    public void Stun(float duration)
    {
        StunServerRpc(duration);
    }

    public bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (IsDead)
            return false;
        if (force <= 0)
            return false;
        StartDyingServerRpc();
        return true;
    }

    public void Kill(bool destroy)
    {
        if (destroy && IsOwner)
            NetworkObject.Despawn();

        StartDyingServerRpc();
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
        animator.CrossFadeInFixedTime("Extending", 0.5f);
    }

    internal bool HasGrabbedObject => !(grabbedItem is null && grabbedPlayer is null && grabbedBody is null && grabbedEnemy is null);

    internal bool CanGrab => !HasGrabbedObject && (state == State.Extending || state == State.Idle);

    internal bool IsDead => state == State.Dead;

    private bool CanGrabPlayer(PlayerControllerB player)
    {
        if (!player.IsOwner)
            return false;
        if (player.isPlayerDead)
            return false;
        return true;
    }

    private bool CanGrabItem(GrabbableObject item)
    {
        if (item.itemProperties.twoHandedAnimation && item is not RagdollGrabbableObject)
            return false;
        return true;
    }

    private bool CanGrabEnemy(EnemyAI enemy)
    {
        if (!enemy.IsOwner)
            return false;
        if (!(enemy is MaskedPlayerEnemy || enemy is FlowerSnakeEnemy || enemy is CentipedeAI))
            return false;
        return true;
    }

    public void TryGrabPlayerOrHeldItem(PlayerControllerB player)
    {
        if (!CanGrab)
            return;
        if (!CanGrabPlayer(player))
            return;

        foreach (var enemy in PatchEnemyAI.AllEnemies)
        {
            if (enemy.isEnemyDead)
                continue;
            if (enemy is FlowerSnakeEnemy flowerSnake && flowerSnake.clingingToPlayer == player && flowerSnake.clingPosition == 4)
            {
                GrabEnemy(flowerSnake);
                return;
            }
            if (enemy is CentipedeAI centipede && centipede.clingingToPlayer == player)
            {
                GrabEnemy(centipede);
                return;
            }
        }

        var heldItem = player.currentlyHeldObjectServer;
        if (heldItem != null && heldItem.IsOwner && CanGrabItem(heldItem))
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
                GrabItem(heldItem);
                return;
            }
        }

        GrabPlayer(player);
    }

    private int GetNearestSegmentToPosition(Vector3 position)
    {
        var closestDistanceSqr = float.PositiveInfinity;
        var closestSegmentIndex = tongueSegments.Length - 1;

        for (var i = 0; i < tongueSegments.Length; i++)
        {
            var segmentCollider = tongueSegmentColliders[i];
            var segmentTransform = segmentCollider.transform;
            var segmentCenter = segmentTransform.TransformPoint(segmentCollider.center);

            var distanceSqr = (position - segmentCenter).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestSegmentIndex = i;
            }
        }

        return closestSegmentIndex;
    }

    private Vector3 GetColliderCenter(Collider collider)
    {
        if (collider is BoxCollider box)
            return box.center;
        if (collider is SphereCollider sphere)
            return sphere.center;
        if (collider is CapsuleCollider capsule)
            return capsule.center;
        if (collider is CharacterController character)
            return character.center;
        return Vector3.zero;
    }

    private void GetAttachmentPosition(Vector3 rootPosition, Vector3 hitPosition, Collider collider, out Rigidbody attachSegment, out Vector3 attachPosition, out Vector3 holderPosition, out Vector3 centerOfMass, out float tongueOffset)
    {
        var closestIndex = GetNearestSegmentToPosition(hitPosition);

        attachSegment = tongueSegments[closestIndex];
        attachPosition = tongueSegmentColliders[closestIndex].ClosestPoint(hitPosition);

        var rootOffset = rootPosition - hitPosition;
        if (collider != null)
        {
            var contactPoint = collider.ClosestPoint(attachPosition);
            contactPoint = Vector3.Lerp(hitPosition, contactPoint, holderPositionScale);
            rootOffset = rootPosition - contactPoint;
        }
        holderPosition = attachPosition + rootOffset;

        centerOfMass = attachPosition;

        if (collider != null)
            centerOfMass = collider.transform.TransformPoint(GetColliderCenter(collider)) - rootPosition + holderPosition;

        var segmentPosition = attachSegment.transform.position;
        var segmentUp = attachSegment.transform.right;
        tongueOffset = tongueSegmentMouthOffsets[closestIndex] + Vector3.Dot(attachPosition - segmentPosition, segmentUp);
    }

    private void DisableAllPhysics()
    {
        DisableHolderPhysics();
        foreach (var tongueSegment in tongueSegments)
            tongueSegment.isKinematic = true;
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
        GrabItemOnClient(item);
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
        if (!CanGrabItem(item))
            return;

        if (item.playerHeldBy is { } player)
            RemoveItemFromPlayerInventory(player, player.currentItemSlot);

        var itemParent = item.parentObject ?? item.transform;
        var itemCollider = item.GetComponent<Collider>();
        GetAttachmentPosition(itemParent.position, item.transform.position, itemCollider, out var segment, out var attachPosition, out var holderPosition, out var centerOfMass, out eatingTongueOffset);
        eatingTongueOffset += eatDistance;

        dummyObject.SetPositionAndRotation(attachPosition, Quaternion.identity);
        dummyObjectJoint.connectedBody = segment;
        SetCenterOfMassToWorldPosition(dummyObjectBody, centerOfMass);
        dummyObjectBody.angularDrag = defaultAngularDrag;
        holder.SetPositionAndRotation(holderPosition, itemParent.rotation);
        centeredHolderLocalPosition = Vector3.zero;
        EnableHolderPhysics();

        item.parentObject = holder;
        grabbedItem = item;
        item.EnablePhysics(false);

        if (item is RagdollGrabbableObject ragdoll)
            ragdoll.heldByEnemy = true;

        sounds.PlayGrabItemSound();

        BeginPulling();
    }

    private void GrabPlayer(PlayerControllerB player)
    {
        if (!CanGrabPlayer(player))
            return;
        GrabPlayerOnClient(player);
        GrabPlayerServerRpc(player);
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
        GrabPlayerOnClient(player);
    }

    public void GrabPlayerOnClient(PlayerControllerB player)
    {
        if (!CanGrab)
            return;

        grabbedPlayerPreviousParent = player.transform.parent;

        var rootPosition = player.transform.position;
        var eyePosition = player.gameplayCamera.transform.position;
        GetAttachmentPosition(rootPosition, eyePosition, player.playerCollider, out var segment, out var attachPosition, out var holderPosition, out var centerOfMass, out eatingTongueOffset);
        eatingTongueOffset += eatDistance;

        dummyObject.SetPositionAndRotation(attachPosition, Quaternion.identity);
        dummyObjectJoint.connectedBody = segment;
        SetCenterOfMassToWorldPosition(dummyObjectBody, centerOfMass);
        dummyObjectBody.angularDrag = 5;
        holder.SetPositionAndRotation(holderPosition, grabbedPlayerPreviousParent.transform.rotation);
        centeredHolderLocalPosition = rootPosition - eyePosition;
        EnableHolderPhysics();

        grabbedPlayer = player;
        grabbedPlayer.transform.SetParent(holder, false);
        PatchPlayerControllerB.SetPlayerPositionLocked(player, true);

        sounds.PlayGrabPlayerSound();

        BeginPulling();
    }

    public void GrabEnemy(EnemyAI enemy)
    {
        if (!CanGrabEnemy(enemy))
            return;

        GrabEnemyOnClient(enemy);
        GrabEnemyServerRpc(enemy);
    }

    [ServerRpc(RequireOwnership = false)]
    private void GrabEnemyServerRpc(NetworkBehaviourReference enemyReference)
    {
        GrabEnemyClientRpc(enemyReference);
    }

    [ClientRpc]
    private void GrabEnemyClientRpc(NetworkBehaviourReference enemyReference)
    {
        if (!enemyReference.TryGet(out EnemyAI enemy))
        {
            BlackMesaInterior.Logger.LogError($"{nameof(GrabEnemyClientRpc)} called with invalid enemy reference.");
            return;
        }
        GrabEnemyOnClient(enemy);
    }

    private static Vector3 GetEnemyAttachmentPoint(EnemyAI enemy)
    {
        if (enemy is MaskedPlayerEnemy)
            return enemy.eye.position;
        return enemy.transform.position;
    }

    private static Collider GetEnemyCollider(EnemyAI enemy)
    {
        return enemy.GetComponentInChildren<EnemyAICollisionDetect>()?.GetComponent<Collider>();
    }

    private static void SetEnemyGrabbedAnimations(EnemyAI enemy)
    {
        if (enemy is MaskedPlayerEnemy maskedEnemy)
        {
            maskedEnemy.creatureAnimator.SetBool("HandsOut", false);
            maskedEnemy.creatureAnimator.SetBool("IsMoving", false);
            maskedEnemy.creatureAnimator.SetBool("Running", false);
            maskedEnemy.CancelSpecialAnimationWithPlayer();
            return;
        }
        if (enemy is FlowerSnakeEnemy flowerSnake)
        {
            flowerSnake.StopClingingOnLocalClient();
            return;
        }
        if (enemy is CentipedeAI centipede)
        {
            if (centipede.clingingToPlayer != null)
                centipede.StopClingingToPlayer(false);
            return;
        }
    }

    private void GrabEnemyOnClient(EnemyAI enemy)
    {
        if (!CanGrab)
            return;
        if (!enemy.gameObject.activeInHierarchy)
            return;

        var rootPosition = enemy.transform.position;
        var eyePosition = GetEnemyAttachmentPoint(enemy);
        var enemyCollider = GetEnemyCollider(enemy);
        GetAttachmentPosition(rootPosition, eyePosition, enemyCollider, out var segment, out var attachPosition, out var holderPosition, out var centerOfMass, out eatingTongueOffset);
        eatingTongueOffset += eatDistance;

        dummyObject.SetPositionAndRotation(attachPosition, Quaternion.identity);
        dummyObjectJoint.connectedBody = segment;
        SetCenterOfMassToWorldPosition(dummyObjectBody, centerOfMass);
        dummyObjectBody.angularDrag = defaultAngularDrag;
        holder.SetPositionAndRotation(holderPosition, enemy.transform.rotation);
        centeredHolderLocalPosition = rootPosition - eyePosition;
        EnableHolderPhysics();

        grabbedEnemy = enemy;
        var locker = enemy.gameObject.AddComponent<LockPosition>();
        locker.target = holder;
        grabbedEnemy.SetClientCalculatingAI(false);
        grabbedEnemy.enabled = false;
        SetEnemyGrabbedAnimations(grabbedEnemy);

        sounds.PlayGrabItemSound();

        BeginPulling();
    }

    public void BeginPulling()
    {
        sounds.PlayContactSound();
        StartCoroutine(BeginPullingCoroutine());
    }

    private IEnumerator BeginPullingCoroutine()
    {
        SetState(State.Pulling);
        yield return new WaitForSeconds(0.2f);

        if (state == State.Pulling)
            animator.CrossFadeInFixedTime("Pulling", 0.25f);
    }

    public void YankTongue(float distance)
    {
        if (IsDead)
            return;
        if (state != State.Pulling)
            return;

        targetTongueOffset = currentTongueOffset - distance;
    }

    public void SetTargetTongueOffset(float distance)
    {
        if (IsDead)
            return;

        targetTongueOffset = distance;
    }

    private void Update()
    {
        if (centeringHolderPosition)
            holder.localPosition = Vector3.Lerp(holder.localPosition, centeredHolderLocalPosition, Time.deltaTime);
        if (centeringDummyObjectRotation)
            dummyObject.rotation = Quaternion.Lerp(dummyObject.rotation, Quaternion.identity, 4 * Time.deltaTime);

        if (state == State.Flinching)
        {
            flinchTimeLeft -= Time.deltaTime;

            if (flinchTimeLeft <= 0)
                DropTongue();
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

        animator.CrossFadeInFixedTime("Idle", 0.25f);
        SetState(State.Idle);
    }

    public void BeginEatingGrabbedObject()
    {
        if (!IsOwner)
            return;
        BeginEatingGrabbedObjectClientRpc();
    }

    [ClientRpc]
    private void BeginEatingGrabbedObjectClientRpc()
    {
        if (state != State.Pulling)
            return;

        if (grabbedPlayer != null || grabbedBody != null)
        {
            StartHumanoidEatingAnimation();
        }
        else if (grabbedEnemy != null)
        {
            if (grabbedEnemy is MaskedPlayerEnemy)
                StartHumanoidEatingAnimation();
            else
                StartFastEatingAnimation();
        }
        else if (grabbedItem != null)
        {
            StartFastEatingAnimation();
        }
        else
        {
            StunOnClient(0.25f);
            return;
        }

        targetTongueOffset = eatingTongueOffset;
        centeringHolderPosition = true;
        SetState(State.Eating);
    }

    private void StartFastEatingAnimation()
    {
        animator.CrossFadeInFixedTime("Eat Item", 0.25f);
    }

    private void StartHumanoidEatingAnimation()
    {
        animator.CrossFadeInFixedTime("Begin Attacking Humanoid", 0.25f);
    }

    public void AttachHolderToMouth()
    {
        DisableHolderPhysics();
        dummyObjectJoint.connectedBody = null;
        dummyObject.SetParent(mouthAttachment, true);
        dummyObject.localPosition = Vector3.zero;
        if (grabbedItem == null)
            centeringDummyObjectRotation = true;
    }

    public void BiteGrabbedHumanoid(int damage)
    {
        if (grabbedPlayer == null)
        {
            SwallowGrabbedHumanoid();
            return;
        }
        if (!grabbedPlayer.IsOwner)
            return;
        if (IsDead)
            return;

        if (grabbedPlayer.criticallyInjured || grabbedPlayer.isPlayerDead || !grabbedPlayer.AllowPlayerDeath())
        {
            grabbedPlayer.DamagePlayer(0, hasDamageSFX: true, callRPC: true, CauseOfDeath.Crushing);
            SwallowGrabbedHumanoid();
            return;
        }

        grabbedPlayer.DamagePlayer(damage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Crushing);
    }

    private void SwallowGrabbedHumanoid()
    {
        SwallowGrabbedHumanoidOnClient();
        SwallowGrabbedHumanoidServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SwallowGrabbedHumanoidServerRpc()
    {
        SwallowGrabbedHumanoidClientRpc();
    }

    [ClientRpc]
    private void SwallowGrabbedHumanoidClientRpc()
    {
        SwallowGrabbedHumanoidOnClient();
    }

    private void SwallowGrabbedHumanoidOnClient()
    {
        animator.SetTrigger("Swallow Humanoid");
    }

    public void CrushTarget()
    {
        if (grabbedPlayer != null && grabbedPlayer.IsOwner)
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
        if (grabbedPlayer == null)
            return;

        const string methodName = $"{nameof(Barnacle)}.{nameof(AttachRagdoll)}()";

        if (grabbedPlayer.deadBody == null)
        {
            BlackMesaInterior.Logger.LogWarning($"{methodName}: Grabbed player {grabbedPlayer} has no ragdoll.");
            return;
        }

        var head = grabbedPlayer.deadBody.transform.Find("spine.001/spine.002/spine.003/spine.004");

        if (head == null)
        {
            grabbedPlayer.deadBody.DeactivateBody(false);
            return;
        }
        if (!head.TryGetComponent<Rigidbody>(out var headRigidbody))
        {
            BlackMesaInterior.Logger.LogWarning($"{methodName}: {grabbedPlayer}'s ragdoll's head has no rigidbody.");
            return;
        }

        // Prevent collision from yeeting the body all over.
        foreach (var rigidBody in grabbedPlayer.deadBody.bodyParts)
            rigidBody.detectCollisions = false;

        PatchPlayerControllerB.SetPlayerPositionLocked(grabbedPlayer, false);
        grabbedPlayerPreviousParent = null;

        holder.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        centeredHolderLocalPosition = Vector3.zero;
        holderJoint.connectedBody = headRigidbody;
        grabbedBody = grabbedPlayer.deadBody;
        grabbedPlayer = null;
    }

    public void SwallowGrabbedObject()
    {
        centeringHolderPosition = false;
        centeringDummyObjectRotation = false;

        if (grabbedItem != null)
        {
            grabbedItem.parentObject = itemStash;

            grabbedItem.EnableItemMeshes(false);

            if (grabbedItem is RagdollGrabbableObject ragdollItem)
                ragdollItem.ragdoll.DeactivateBody(false);
            else
                eatenItems.Add(grabbedItem);
            grabbedItem = null;
        }
        if (grabbedEnemy != null)
        {
            if (grabbedEnemy.IsOwner)
                grabbedEnemy.KillEnemyOnOwnerClient(true);
            DropEnemy();
            // Centipede ignores the destroy parameter, so deactivate it as well.
            grabbedEnemy.gameObject.SetActive(false);
            grabbedEnemy = null;
        }
        if (grabbedBody != null)
        {
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
    }

    public void SpitChewedGuts()
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

    private void DropRagdoll()
    {
        if (grabbedBody == null)
            return;

        foreach (var rigidBody in grabbedBody.bodyParts)
            rigidBody.detectCollisions = true;

        holderJoint.connectedBody = null;
    }

    private void DropEnemy()
    {
        if (grabbedEnemy == null)
            return;

        Destroy(grabbedEnemy.GetComponent<LockPosition>());
        grabbedEnemy.SetClientCalculatingAI(true);
        grabbedEnemy.enabled = true;

        ResetHolder();
    }

    public static void OnPlayerTeleported(PlayerControllerB player)
    {
        foreach (var barnacle in barnacles)
        {
            if (player.isPlayerDead)
            {
                if (player.deadBody != null && barnacle.grabbedBody == player.deadBody)
                {
                    barnacle.DropRagdoll();
                    barnacle.grabbedBody = null;
                }
            }
            else if (player == barnacle.grabbedPlayer)
            {
                barnacle.DropPlayer();
                barnacle.grabbedPlayer = null;
            }
        }
    }

    private void ResetHolder()
    {
        DisableHolderPhysics();
        dummyObjectJoint.connectedBody = null;
        dummyObject.position = transform.position;
        dummyObject.rotation = Quaternion.identity;
    }

    [ServerRpc(RequireOwnership = false)]
    private void StunServerRpc(float duration)
    {
        StunClientRpc(duration);
    }

    [ClientRpc]
    private void StunClientRpc(float duration)
    {
        StunOnClient(duration);
    }

    private void PlayFlinchAnimation()
    {
        animator.CrossFadeInFixedTime($"Flinch {animationRandomizer.Next(2) + 1}", 0.125f);
    }

    private void StunOnClient(float duration)
    {
        SetState(State.Flinching);
        flinchTimeLeft = duration;

        animator.ResetTrigger("Swallow Humanoid");
        animator.ResetTrigger("Finish Eating");
        PlayFlinchAnimation();

        sounds.PlayFlinchSound();

        DropGrabbedObjectsOnClient();
    }

    private void DropGrabbedObjectsOnClient()
    {
        if (grabbedItem != null)
        {
            DropItem(grabbedItem);
            ResetHolder();
        }
        grabbedItem = null;

        DropPlayer();
        grabbedPlayer = null;

        DropRagdoll();
        grabbedBody = null;

        DropEnemy();
        grabbedEnemy = null;
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
        SetState(State.Dead);

        sounds.PlayFlinchSound();
        PlayFlinchAnimation();

        yield return new WaitForSeconds(0.5f);

        animator.CrossFadeInFixedTime($"Death {animationRandomizer.Next(2) + 1}", 0.25f);

        yield return new WaitForSeconds(1);

        DropGrabbedObjectsOnClient();

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
        DropGrabbedObjectsOnClient();

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
