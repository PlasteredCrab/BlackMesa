using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine;

namespace BlackMesa.Components;

public abstract class StationBase : NetworkBehaviour
{
    protected enum AnimationState
    {
        None,
        Active,
        Exiting,
    }

    [Flags]
    protected enum AnimationFlags
    {
        None = 0,
        PlayedStartupAudio = 1 << 1,
        PlayedActiveAudio = 1 << 2,
        StoppedSpecialAnimation = 1 << 3,
        StartedSpecialAnimation = 1 << 4,
        StoppedActiveAudio = 1 << 5,
    }

    public InteractTrigger triggerScript;

    public AudioSource startupAudio;
    public AudioSource activeAudio;
    public AudioSource depletedAudio;
    public Animator animator;
    public Renderer renderer;

    public float emissiveMultiplier = 1;
    public int[] emissiveMaterialIndices;

    public float maxCapacity = 100;

    // Animation timing
    public float startupAudioTime = 0.25f;
    public float activeDelayTime = 0.5f;
    public float activeAudioStartTime = 0.75f;
    public float activeAudioEndTime = 0.4f;
    public float activeEndTime = 0.65f;

    public bool stopActiveAudioInstantly = false;

    public float capacitySyncInterval = 0.25f;

    private Material[] materials;
    private Color[] materialEmissives;
    private float prevEmissiveMultiplier = 0;
    private int emissiveColorPropertyID = Shader.PropertyToID("_EmissiveColor");

    protected bool isActiveOnLocalClient = false;

    protected float remainingCapacity;
    private float prevRemainingCapacity = -1;
    private float lastCapacitySync;

    private float timeUntilActive = 0;
    private float timeUntilInactive = 0;

    protected PlayerControllerB playerInteracting;
    protected AnimationState state = AnimationState.None;
    protected float stateStartTime;
    protected AnimationFlags stateFlags = AnimationFlags.None;

    private InputAction interactAction;

    private struct CapacityCheckpoint(float time, float capacity)
    {
        internal float time = time;
        internal float capacity = capacity;
    }

    private CapacityCheckpoint prevCapacityCheckpoint;
    private Queue<CapacityCheckpoint> capacityInterpolationQueue = [];
    private float capacityInterpolationDelay;
    private bool addedInitialCheckpoint = false;

    protected virtual void Start()
    {
        remainingCapacity = maxCapacity;

        interactAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Interact");

        prevCapacityCheckpoint = new CapacityCheckpoint(Time.time, remainingCapacity);

        materials = new Material[emissiveMaterialIndices.Length];
        materialEmissives = new Color[emissiveMaterialIndices.Length];
        for (var i = 0; i < emissiveMaterialIndices.Length; i++)
        {
            var material = renderer.materials[emissiveMaterialIndices[i]];
            materials[i] = material;
            materialEmissives[i] = material.GetColor(emissiveColorPropertyID);
        }
    }

    private void SetAnimationState(AnimationState state)
    {
        if (this.state == state)
            return;
        this.state = state;
        stateStartTime = Time.time;
        if (state == AnimationState.None)
            stateFlags = AnimationFlags.None;
    }

    // Handles the InteractTrigger.onInteractEarly event
    public void SetActivatedByLocalPlayer()
    {
        isActiveOnLocalClient = true;
        SetActivatedByPlayer(GameNetworkManager.Instance.localPlayerController);
    }

    // Handles the InteractTrigger.onInteractEarlyOtherClients event
    public void SetActivatedByOtherClient(PlayerControllerB player)
    {
        if (player == GameNetworkManager.Instance.localPlayerController)
            return;

        isActiveOnLocalClient = false;
        SetActivatedByPlayer(player);
    }

    private void SetActivatedByPlayer(PlayerControllerB player)
    {
        timeUntilActive = activeDelayTime;
        timeUntilInactive = float.PositiveInfinity;

        playerInteracting = player;

        capacityInterpolationDelay = capacitySyncInterval;
        OnActivatedByPlayer(player, ref capacityInterpolationDelay);

        SetAnimationState(AnimationState.Active);

        addedInitialCheckpoint = false;
    }

    protected abstract void OnActivatedByPlayer(PlayerControllerB player, ref float capacityInterpolationDelay);

    private bool HasFinishedActivating()
    {
        return timeUntilActive <= 0;
    }

    private void Update()
    {
        UpdateInteractability();

        timeUntilActive -= Time.deltaTime;

        if (state != AnimationState.None && !addedInitialCheckpoint && HasFinishedActivating())
        {
            capacityInterpolationQueue.Enqueue(new CapacityCheckpoint(Time.time, remainingCapacity));
            addedInitialCheckpoint = true;
        }

        TickIfActive();

        switch (state)
        {
            case AnimationState.Active:
                DoActiveAnimationTick();
                break;
            case AnimationState.Exiting:
                DoExitingAnimationTick();
                break;
        }

        AnimateCapacity();
    }

    private void UpdateInteractability()
    {
        if (remainingCapacity <= 0)
        {
            triggerScript.disabledHoverTip = "Depleted";
            triggerScript.interactable = false;
            return;
        }

        UpdateInteractabilityWithCapacity();
    }

    protected abstract void UpdateInteractabilityWithCapacity();

    protected enum TickResult
    {
        Continue,
        Deactivate,
    }

    private void TickIfActive()
    {
        if (!isActiveOnLocalClient)
            return;

        if (!HasFinishedActivating())
            return;

        bool deactivate = false;

        // Delay exiting heal to give time for the hand to pull away.
        if (timeUntilInactive == float.PositiveInfinity)
        {
            if (!interactAction.IsPressed())
            {
                timeUntilInactive = activeEndTime;
                StartExitingAnimationServerRpc();
            }
        }
        else
        {
            timeUntilInactive -= Time.deltaTime;
            if (timeUntilInactive < 0)
                deactivate = true;
        }

        if (DoActiveTick() == TickResult.Deactivate)
        {
            deactivate = true;
            StartExitingAnimationServerRpc();
        }

        if (remainingCapacity == 0)
        {
            deactivate = true;
            StartExitingAnimationServerRpc();
        }

        bool capacityChanged = prevRemainingCapacity != remainingCapacity;
        bool capacitySynced = false;
        if (capacityChanged)
        {
            prevRemainingCapacity = remainingCapacity;
            UpdateCapacityAnimation();
            var timeSinceServerUpdate = Time.time - lastCapacitySync;
            if (timeSinceServerUpdate >= capacitySyncInterval)
            {
                SyncCapacity();
                capacitySynced = true;
            }
        }

        if (deactivate)
        {
            isActiveOnLocalClient = false;
            if (capacityChanged && !capacitySynced)
                SyncCapacity();
            OnActiveTickingEnded();
        }
    }

    protected abstract TickResult DoActiveTick();

    protected abstract void OnActiveTickingEnded();

    protected void ConsumeCapacity(float amount)
    {
        if (amount > remainingCapacity)
            throw new ArgumentOutOfRangeException("amount", $"Capacity to consume {amount:.4} was greater than remaining capacity {remainingCapacity:.4}");

        remainingCapacity -= amount;
    }

    private void SyncCapacity()
    {
        UpdateCapacityServerRpc(remainingCapacity);
        lastCapacitySync = Time.time;
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateCapacityServerRpc(float capacity)
    {
        if (IsServer)
            capacity = Math.Min(capacity, remainingCapacity);
        UpdateCapacityClientRpc(capacity);
    }

    [ClientRpc]
    private void UpdateCapacityClientRpc(float capacity)
    {
        if (isActiveOnLocalClient)
            return;
        remainingCapacity = capacity;
        UpdateCapacityAnimation();
    }

    private void UpdateCapacityAnimation()
    {
        prevRemainingCapacity = remainingCapacity;

        capacityInterpolationQueue.Enqueue(new CapacityCheckpoint(Time.time, remainingCapacity));

        if (remainingCapacity <= 0)
        {
            StopActiveAudioOnClient(immediate: true);
            depletedAudio.Play();
            animator.SetTrigger("Shuttered");
        }
        else
        {
            animator.ResetTrigger("Shuttered");
        }
    }

    private float GetUsedCapacityFraction(float remainingCapacity)
    {
        return 1 - Math.Min(remainingCapacity / maxCapacity, 1);
    }

    private void AnimateCapacity()
    {
        var currentTime = Time.time - capacityInterpolationDelay;

        while (capacityInterpolationQueue.TryPeek(out var nextCheckpoint) && nextCheckpoint.time <= currentTime)
            prevCapacityCheckpoint = capacityInterpolationQueue.Dequeue();

        if (!capacityInterpolationQueue.TryPeek(out var target))
        {
            animator.SetFloat("UsedCapacity", GetUsedCapacityFraction(prevCapacityCheckpoint.capacity));
            return;
        }

        var delta = Mathf.Clamp01((currentTime - prevCapacityCheckpoint.time) / (target.time - prevCapacityCheckpoint.time));
        var displayUsedCapacity = GetUsedCapacityFraction(Mathf.Lerp(prevCapacityCheckpoint.capacity, target.capacity, delta));
        animator.SetFloat("UsedCapacity", displayUsedCapacity);
    }

    private void DoActiveAnimationTick()
    {
        var stateTime = Time.time - stateStartTime;

        if (!stateFlags.HasFlag(AnimationFlags.PlayedStartupAudio) && stateTime >= startupAudioTime)
        {
            startupAudio.Play();
            stateFlags |= AnimationFlags.PlayedStartupAudio;
        }

        if (!stateFlags.HasFlag(AnimationFlags.PlayedActiveAudio) && stateTime >= activeAudioStartTime)
        {
            if (!stopActiveAudioInstantly)
                activeAudio.loop = true;
            activeAudio.Play();
            stateFlags |= AnimationFlags.PlayedActiveAudio;
        }

        // Stop the animation after 1 second to hold the charging animation in the middle.
        if (stateTime < 1)
            return;
        if (!stateFlags.HasFlag(AnimationFlags.StoppedSpecialAnimation))
        {
            playerInteracting.playerBodyAnimator.speed = 0;
            stateFlags |= AnimationFlags.StoppedSpecialAnimation;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    internal void StartExitingAnimationServerRpc()
    {
        StartExitingAnimationClientRpc();
    }

    [ClientRpc]
    internal void StartExitingAnimationClientRpc()
    {
        SetAnimationState(AnimationState.Exiting);
    }

    protected void StopActiveAudioOnClient(bool immediate = false)
    {
        if (immediate || stopActiveAudioInstantly)
            activeAudio.Stop();
        else
            activeAudio.loop = false;
        stateFlags |= AnimationFlags.StoppedActiveAudio;
    }

    private void DoExitingAnimationTick()
    {
        var stateTime = Time.time - stateStartTime;

        if (!stateFlags.HasFlag(AnimationFlags.StartedSpecialAnimation))
        {
            playerInteracting.playerBodyAnimator.speed = 1;
            stateFlags |= AnimationFlags.StartedSpecialAnimation;
        }

        if (!stateFlags.HasFlag(AnimationFlags.StoppedActiveAudio) && stateTime >= activeAudioEndTime)
            StopActiveAudioOnClient();

        if (stateTime < 1)
            return;
        triggerScript.StopSpecialAnimation();
        SetAnimationState(AnimationState.None);
    }

    private void LateUpdate()
    {
        if (emissiveMultiplier == prevEmissiveMultiplier)
            return;

        for (var i = 0; i < materials.Length; i++)
            materials[i].SetColor(emissiveColorPropertyID, materialEmissives[i] * emissiveMultiplier);

        prevEmissiveMultiplier = emissiveMultiplier;
    }
}
