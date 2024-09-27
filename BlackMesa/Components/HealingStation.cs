using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackMesa.Components;

public class HealingStation : NetworkBehaviour
{
    private enum AnimationState
    {
        None,
        Active,
        Exiting,
    }

    [Flags]
    private enum AnimationFlags
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

    public int maxCapacity = 100;
    private int remainingCapacity;

    // Animation timing
    public float startupAudioTime = 0.25f;
    public float activeDelayTime = 0.5f;
    public float activeAudioStartTime = 0.75f;
    public float activeAudioEndTime = 0.4f;
    public float activeEndTime = 0.65f;

    public Material backlightMaterial;
    public float backlightEmissive = 1;
    private float prevBacklightEmissive = 0;
    private int emissiveColorPropertyID = Shader.PropertyToID("_EmissiveColor");

    private bool isActiveOnLocalClient = false;
    private float timeUntilActive = 0;
    private float timeUntilInactive = 0;

    private PlayerControllerB playerInteracting;
    private AnimationState state = AnimationState.None;
    private float stateStartTime;
    private AnimationFlags stateFlags = AnimationFlags.None;

    private InputAction interactAction;

    private struct CapacityCheckpoint(float time, int capacity)
    {
        internal float time = time;
        internal int capacity = capacity;
    }

    private CapacityCheckpoint prevCapacityCheckpoint;
    private Queue<CapacityCheckpoint> capacityInterpolationQueue = [];
    private float capacityInterpolationDelay;
    private bool addedInitialCheckpoint = false;

    private int maximumPlayerHealth;

    private float healthPerSecond = 10f;
    private float healthRemainder = 0f;

    private float lastServerHealthUpdate = 0f;
    private float serverHealthUpdateInterval = 0.25f;

    private void Start()
    {
        remainingCapacity = maxCapacity;

        maximumPlayerHealth = GameNetworkManager.Instance.localPlayerController.health;

        interactAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Interact");

        prevCapacityCheckpoint = new CapacityCheckpoint(Time.time, remainingCapacity);
    }

    private void SetAnimationState(AnimationState state)
    {
        if (this.state == state)
            return;
        this.state = state;
        stateStartTime = Time.time;
        stateFlags = AnimationFlags.None;
    }

    // Handles the InteractTrigger.onInteractEarly event
    public void SetActivatedByLocalPlayer()
    {
        isActiveOnLocalClient = true;
        capacityInterpolationDelay = 1 / healthPerSecond;

        SetActivatedByPlayer(GameNetworkManager.Instance.localPlayerController);
    }

    // Handles the InteractTrigger.onInteractEarlyOtherClients event
    public void SetActivatedByOtherClient(PlayerControllerB player)
    {
        if (player == GameNetworkManager.Instance.localPlayerController)
            return;

        isActiveOnLocalClient = false;
        capacityInterpolationDelay = serverHealthUpdateInterval;
        SetActivatedByPlayer(player);
    }

    private void SetActivatedByPlayer(PlayerControllerB player)
    {
        timeUntilActive = activeDelayTime;
        timeUntilInactive = float.PositiveInfinity;

        playerInteracting = player;

        SetAnimationState(AnimationState.Active);

        addedInitialCheckpoint = false;
    }

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
        if (GameNetworkManager.Instance.localPlayerController.health >= maximumPlayerHealth)
        {
            triggerScript.disabledHoverTip = "Already full health";
            triggerScript.interactable = false;
            return;
        }

        triggerScript.interactable = true;
    }

    private void TickIfActive()
    {
        if (!isActiveOnLocalClient)
            return;

        if (!HasFinishedActivating())
            return;

        // Calculate healing amount based on deltaTime and healthPerSecond.
        float healAmount = healthPerSecond * Time.deltaTime + healthRemainder;
        healAmount = Math.Min(healAmount, remainingCapacity);

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

        // Add the integer portion of the healing amount and save the remainder.
        var originalHealth = playerInteracting.health;
        healthRemainder = healAmount % 1;
        playerInteracting.health += (int)healAmount;

        // Cap the player's health at 100.
        if (playerInteracting.health >= maximumPlayerHealth)
        {
            playerInteracting.health = maximumPlayerHealth;
            deactivate = true;
            StartExitingAnimationServerRpc();
        }

        var healthDelta = playerInteracting.health - originalHealth;

        if (healthDelta > 0)
        {
            HUDManager.Instance.UpdateHealthUI(playerInteracting.health, hurtPlayer: false);
            if (playerInteracting.health >= 10)
                playerInteracting.MakeCriticallyInjured(false);
        }

        var newCapacity = remainingCapacity - healthDelta;
        if (newCapacity <= 0)
        {
            newCapacity = 0;
            deactivate = true;
            StartExitingAnimationServerRpc();
        }

        // Handle capacity changing on the local client every frame to update the animation.
        SetCapacityOnLocalClient(newCapacity);

        // Ensure that all other clients see the health modification on an interval and after healing ends.
        if (deactivate || Time.time - lastServerHealthUpdate > serverHealthUpdateInterval)
        {
            UpdatePlayerHealthAndCapacity();
            lastServerHealthUpdate = Time.time;
        }

        if (deactivate)
        {
            isActiveOnLocalClient = false;
            healthRemainder = 0;
        }
    }

    internal void UpdatePlayerHealthAndCapacity()
    {
        if (playerInteracting.IsOwner)
            UpdatePlayerHealthServerRpc((int)playerInteracting.playerClientId, playerInteracting.health, playerInteracting.health >= maximumPlayerHealth, remainingCapacity);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerHealthServerRpc(int playerID, int health, bool fullyHealed, int capacity)
    {
        if (IsServer)
            capacity = Math.Min(capacity, remainingCapacity);
        UpdatePlayerHealthClientRpc(playerID, health, fullyHealed, capacity);
    }

    [ClientRpc]
    private void UpdatePlayerHealthClientRpc(int playerID, int health, bool fullyHealed, int capacity)
    {
        var player = StartOfRound.Instance.allPlayerScripts[playerID];
        if (player.IsOwner)
            return;

        player.DamageOnOtherClients(0, health);
        if (fullyHealed)
            StopActiveAudioOnClient();

        SetCapacityOnLocalClient(capacity);
    }

    private void SetCapacityOnLocalClient(int capacity)
    {
        if (capacity != remainingCapacity)
        {
            capacityInterpolationQueue.Enqueue(new CapacityCheckpoint(Time.time, capacity));
            remainingCapacity = capacity;
        }

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

    private void StopActiveAudioOnClient(bool immediate = false)
    {
        activeAudio.loop = false;
        if (immediate)
            activeAudio.Stop();
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
        if (backlightEmissive == prevBacklightEmissive)
            return;
        backlightMaterial.SetColor(emissiveColorPropertyID, new Color(backlightEmissive, backlightEmissive, backlightEmissive, 1));
        prevBacklightEmissive = backlightEmissive;
    }
}
