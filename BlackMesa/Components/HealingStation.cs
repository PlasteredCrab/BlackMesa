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
        Healing,
        Exiting,
    }

    [Flags]
    private enum AnimationFlags
    {
        None = 0,
        PlayedStartupAudio = 1 << 1,
        PlayedHealingAudio = 1 << 2,
        StoppedSpecialAnimation = 1 << 3,
        StartedSpecialAnimation = 1 << 4,
        StoppedHealingAudio = 1 << 5,
    }

    public InteractTrigger triggerScript;

    public AudioSource startupAudio;
    public AudioSource healAudio;
    public AudioSource depletedAudio;
    public Animator healingStationAnimator;

    public int maxHealingCapacity = 100;
    private int remainingHealingCapacity;

    // Animation timing
    public float startupAudioTime = 0.25f;
    public float healingStartTime = 0.5f;
    public float healingAudioStartTime = 0.75f;
    public float healingAudioEndTime = 0.4f;
    public float healingEndTime = 0.65f;

    public Material backlightMaterial;
    public float backlightEmissive = 1;
    private float prevBacklightEmissive = 0;
    private int emissiveColorPropertyID = Shader.PropertyToID("_EmissiveColor");

    private int maximumPlayerHealth;

    private bool isHealing = false;
    private float timeUntilHealingBegins = 0;
    private float timeUntilHealingEnds = 0;

    private PlayerControllerB playerInteracting;
    private AnimationState state = AnimationState.None;
    private float stateStartTime;
    private AnimationFlags stateFlags = AnimationFlags.None;

    private InputAction interactAction;

    private float healthPerSecond = 10f;
    private float healthRemainder = 0f;

    private float lastServerHealthUpdate = 0f;
    private float serverHealthUpdateInterval = 0.25f;

    private struct CapacityCheckpoint(float time, int capacity)
    {
        internal float time = time;
        internal int capacity = capacity;
    }

    private CapacityCheckpoint prevCapacityCheckpoint;
    private Queue<CapacityCheckpoint> capacityInterpolationQueue = [];
    private float capacityInterpolationDelay;
    private bool addedInitialCheckpoint = false;

    private void Start()
    {
        remainingHealingCapacity = maxHealingCapacity;

        maximumPlayerHealth = GameNetworkManager.Instance.localPlayerController.health;

        interactAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Interact");

        prevCapacityCheckpoint = new CapacityCheckpoint(Time.time, remainingHealingCapacity);
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
    public void StartHealingLocalPlayer()
    {
        isHealing = true;
        capacityInterpolationDelay = 1 / healthPerSecond;

        StartHealingAnimationOnPlayer(GameNetworkManager.Instance.localPlayerController);
    }

    // Handles the InteractTrigger.onInteractEarlyOtherClients event
    public void StartAnimation(PlayerControllerB player)
    {
        if (player == GameNetworkManager.Instance.localPlayerController)
            return;

        isHealing = false;
        capacityInterpolationDelay = serverHealthUpdateInterval;
        StartHealingAnimationOnPlayer(player);
    }

    private void StartHealingAnimationOnPlayer(PlayerControllerB player)
    {
        timeUntilHealingBegins = healingStartTime;
        timeUntilHealingEnds = float.PositiveInfinity;

        playerInteracting = player;

        SetAnimationState(AnimationState.Healing);

        addedInitialCheckpoint = false;
    }

    private bool HealingHasBegun()
    {
        return timeUntilHealingBegins <= 0;
    }

    private void Update()
    {
        UpdateInteractability();

        timeUntilHealingBegins -= Time.deltaTime;

        if (state != AnimationState.None && !addedInitialCheckpoint && HealingHasBegun())
        {
            capacityInterpolationQueue.Enqueue(new CapacityCheckpoint(Time.time, remainingHealingCapacity));
            addedInitialCheckpoint = true;
        }

        ApplyHealing();

        switch (state)
        {
            case AnimationState.Healing:
                DoHealingAnimationTick();
                break;
            case AnimationState.Exiting:
                DoExitingAnimationTick();
                break;
        }

        AnimateCapacity();
    }

    private void UpdateInteractability()
    {
        if (remainingHealingCapacity <= 0)
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

    private void ApplyHealing()
    {
        if (!isHealing)
            return;

        if (!HealingHasBegun())
            return;

        // Calculate healing amount based on deltaTime and healthPerSecond.
        float healAmount = healthPerSecond * Time.deltaTime + healthRemainder;
        healAmount = Math.Min(healAmount, remainingHealingCapacity);

        bool healEnded = false;

        // Delay exiting heal to give time for the hand to pull away.
        if (timeUntilHealingEnds == float.PositiveInfinity)
        {
            if (!interactAction.IsPressed())
            {
                timeUntilHealingEnds = healingEndTime;
                StartExitingAnimationServerRpc();
            }
        }
        else
        {
            timeUntilHealingEnds -= Time.deltaTime;
            if (timeUntilHealingEnds < 0)
                healEnded = true;
        }

        // Add the integer portion of the healing amount and save the remainder.
        var originalHealth = playerInteracting.health;
        healthRemainder = healAmount % 1;
        playerInteracting.health += (int)healAmount;

        // Cap the player's health at 100.
        if (playerInteracting.health >= maximumPlayerHealth)
        {
            playerInteracting.health = maximumPlayerHealth;
            healEnded = true;
            StartExitingAnimationServerRpc();
        }

        var healthDelta = playerInteracting.health - originalHealth;

        if (healthDelta > 0)
        {
            HUDManager.Instance.UpdateHealthUI(playerInteracting.health, hurtPlayer: false);
            if (playerInteracting.health >= 10)
                playerInteracting.MakeCriticallyInjured(false);
        }

        var newHealingCapacity = remainingHealingCapacity - healthDelta;
        if (newHealingCapacity <= 0)
        {
            newHealingCapacity = 0;
            healEnded = true;
            StartExitingAnimationServerRpc();
        }

        // Handle capacity changing on the local client every frame to update the animation.
        SetHealingCapacity(newHealingCapacity);

        // Ensure that all other clients see the health modification on an interval and after healing ends.
        if (healEnded || Time.time - lastServerHealthUpdate > serverHealthUpdateInterval)
        {
            UpdatePlayerHealthAndCapacity();
            lastServerHealthUpdate = Time.time;
        }

        if (healEnded)
        {
            isHealing = false;
            healthRemainder = 0;
        }
    }

    internal void UpdatePlayerHealthAndCapacity()
    {
        if (playerInteracting.IsOwner)
            UpdatePlayerHealthServerRpc((int)playerInteracting.playerClientId, playerInteracting.health, playerInteracting.health >= maximumPlayerHealth, remainingHealingCapacity);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerHealthServerRpc(int playerID, int health, bool fullyHealed, int capacity)
    {
        if (IsServer)
            capacity = Math.Min(capacity, remainingHealingCapacity);
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
            StopHealingAudioOnClient();

        SetHealingCapacity(capacity);
    }

    private void SetHealingCapacity(int healingCapacity)
    {
        if (healingCapacity != remainingHealingCapacity)
        {
            capacityInterpolationQueue.Enqueue(new CapacityCheckpoint(Time.time, healingCapacity));
            remainingHealingCapacity = healingCapacity;
        }

        if (remainingHealingCapacity <= 0)
        {
            StopHealingAudioOnClient(immediate: true);
            depletedAudio.Play();
            healingStationAnimator.SetTrigger("Shuttered");
        }
        else
        {
            healingStationAnimator.ResetTrigger("Shuttered");
        }
    }

    private float GetUsedCapacityFraction(float remainingCapacity)
    {
        return 1 - Math.Min(remainingCapacity / maxHealingCapacity, 1);
    }

    private void AnimateCapacity()
    {
        var currentTime = Time.time - capacityInterpolationDelay;

        while (capacityInterpolationQueue.TryPeek(out var nextCheckpoint) && nextCheckpoint.time <= currentTime)
            prevCapacityCheckpoint = capacityInterpolationQueue.Dequeue();

        if (!capacityInterpolationQueue.TryPeek(out var target))
        {
            healingStationAnimator.SetFloat("UsedCapacity", GetUsedCapacityFraction(prevCapacityCheckpoint.capacity));
            return;
        }

        var delta = Mathf.Clamp01((currentTime - prevCapacityCheckpoint.time) / (target.time - prevCapacityCheckpoint.time));
        var displayUsedCapacity = GetUsedCapacityFraction(Mathf.Lerp(prevCapacityCheckpoint.capacity, target.capacity, delta));
        healingStationAnimator.SetFloat("UsedCapacity", displayUsedCapacity);
    }

    private void DoHealingAnimationTick()
    {
        var stateTime = Time.time - stateStartTime;

        if (!stateFlags.HasFlag(AnimationFlags.PlayedStartupAudio) && stateTime >= startupAudioTime)
        {
            startupAudio.Play();
            stateFlags |= AnimationFlags.PlayedStartupAudio;
        }

        if (!stateFlags.HasFlag(AnimationFlags.PlayedHealingAudio) && stateTime >= healingAudioStartTime)
        {
            healAudio.loop = true;
            healAudio.Play();
            stateFlags |= AnimationFlags.PlayedHealingAudio;
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

    private void StopHealingAudioOnClient(bool immediate = false)
    {
        healAudio.loop = false;
        if (immediate)
            healAudio.Stop();
        stateFlags |= AnimationFlags.StoppedHealingAudio;
    }

    private void DoExitingAnimationTick()
    {
        var stateTime = Time.time - stateStartTime;

        if (!stateFlags.HasFlag(AnimationFlags.StartedSpecialAnimation))
        {
            playerInteracting.playerBodyAnimator.speed = 1;
            stateFlags |= AnimationFlags.StartedSpecialAnimation;
        }

        if (!stateFlags.HasFlag(AnimationFlags.StoppedHealingAudio) && stateTime >= healingAudioEndTime)
            StopHealingAudioOnClient();

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
