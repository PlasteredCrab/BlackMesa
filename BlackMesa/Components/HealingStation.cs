using GameNetcodeStuff;
using System;
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
    }

    public InteractTrigger triggerScript;

    public AudioSource startupAudio;
    public AudioSource healAudio;
    public AudioSource depletedAudio;
    public Animator healingStationAnimator;

    public int maxHealingCapacity = 100;
    private int remainingHealingCapacity;

    public float healingDelay = 0.5f;

    private int maximumPlayerHealth;

    private bool isHealing = false;
    private float timeUntilHealingBegins = 0;

    private PlayerControllerB playerInteracting;
    private AnimationState state = AnimationState.None;
    private float stateStartTime;
    private AnimationFlags flags = AnimationFlags.None;

    private InputAction interactAction;

    private float healthPerSecond = 10f;
    private float healthRemainder = 0f;

    private float lastServerHealthUpdate = 0f;
    private float serverHealthUpdateInterval = 0.25f;

    private void Start()
    {
        remainingHealingCapacity = maxHealingCapacity;

        maximumPlayerHealth = GameNetworkManager.Instance.localPlayerController.health;

        interactAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Interact");
    }

    private void SetAnimationState(AnimationState state)
    {
        this.state = state;
        stateStartTime = Time.time;
        flags = AnimationFlags.None;
    }

    // Handles the InteractTrigger.onInteractEarly event
    public void StartHealingLocalPlayer()
    {
        isHealing = true;
        timeUntilHealingBegins = healingDelay;
        playerInteracting = GameNetworkManager.Instance.localPlayerController;
        StartAnimationState();
    }

    // Handles the InteractTrigger.onInteractEarlyOtherClients event
    public void StartAnimation(PlayerControllerB player)
    {
        if (player == GameNetworkManager.Instance.localPlayerController)
            return;
        playerInteracting = player;
        StartAnimationState();
    }

    private void StartAnimationState()
    {
        SetAnimationState(AnimationState.Healing);
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

    private void Update()
    {
        UpdateInteractability();

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
    }

    private void ApplyHealing()
    {
        if (!isHealing)
            return;

        timeUntilHealingBegins -= Time.deltaTime;
        if (timeUntilHealingBegins > 0)
            return;

        // Calculate healing amount based on deltaTime and healthPerSecond.
        float healAmount = healthPerSecond * Time.deltaTime + healthRemainder;
        healAmount = Math.Min(healAmount, remainingHealingCapacity);

        bool healEnded = !interactAction.IsPressed();

        // Add the integer portion of the healing amount and save the remainder.
        var originalHealth = playerInteracting.health;
        healthRemainder = healAmount % 1;
        playerInteracting.health += (int)healAmount;

        // Cap the player's health at 100.
        if (playerInteracting.health >= maximumPlayerHealth)
        {
            playerInteracting.health = maximumPlayerHealth;
            healEnded = true;
        }

        var healthDelta = playerInteracting.health - originalHealth;

        if (healthDelta > 0)
        {
            HUDManager.Instance.UpdateHealthUI(playerInteracting.health, hurtPlayer: false);
            if (playerInteracting.health >= 10)
                playerInteracting.MakeCriticallyInjured(false);
        }

        remainingHealingCapacity -= healthDelta;
        if (remainingHealingCapacity <= 0)
        {
            remainingHealingCapacity = 0;
            healEnded = true;
        }

        // Ensure that all clients see the health modification on an interval and after healing ends.
        if (healEnded || Time.time - lastServerHealthUpdate > serverHealthUpdateInterval)
        {
            OnHealingCapacityChanged();
            UpdatePlayerHealthAndCapacity();
            lastServerHealthUpdate = Time.time;
        }

        if (healEnded)
        {
            isHealing = false;
            healthRemainder = 0;
            StartExitingAnimationServerRpc();
        }
    }

    internal void UpdatePlayerHealthAndCapacity()
    {
        if (playerInteracting.IsOwner)
            UpdatePlayerHealthServerRpc((int)playerInteracting.playerClientId, playerInteracting.health, remainingHealingCapacity);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerHealthServerRpc(int playerID, int health, int capacity)
    {
        if (IsServer)
            capacity = Math.Min(capacity, remainingHealingCapacity);
        UpdatePlayerHealthClientRpc(playerID, health, capacity);
    }

    [ClientRpc]
    private void UpdatePlayerHealthClientRpc(int playerID, int health, int capacity)
    {
        if (isHealing)
            return;

        var player = StartOfRound.Instance.allPlayerScripts[playerID];
        player.DamageOnOtherClients(0, health);

        remainingHealingCapacity = capacity;
        OnHealingCapacityChanged();
    }

    private void OnHealingCapacityChanged()
    {
        if (remainingHealingCapacity <= 0)
        {
            healAudio.Stop();
            depletedAudio.Play();
        }
    }

    private void DoHealingAnimationTick()
    {
        const float startupAudioTime = 0.25f;
        var stateTime = Time.time - stateStartTime;

        if (stateTime > startupAudioTime && !flags.HasFlag(AnimationFlags.PlayedStartupAudio))
        {
            startupAudio.Play();
            flags |= AnimationFlags.PlayedStartupAudio;
        }

        if (stateTime > startupAudioTime + healingDelay && !flags.HasFlag(AnimationFlags.PlayedHealingAudio))
        {
            healAudio.loop = true;
            healAudio.Play();
            healingStationAnimator.SetTrigger("heal");
            flags |= AnimationFlags.PlayedHealingAudio;
        }

        // Stop the animation after 1 second to hold the charging animation in the middle.
        if (stateTime < 1)
            return;
        if (!flags.HasFlag(AnimationFlags.StoppedSpecialAnimation))
        {
            playerInteracting.playerBodyAnimator.speed = 0;
            flags |= AnimationFlags.StoppedSpecialAnimation;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    internal void StartExitingAnimationServerRpc()
    {
        // Start exiting immediately on the client that is interacting.
        if (isHealing)
            SetAnimationState(AnimationState.Exiting);
        StartExitingAnimationClientRpc();
    }

    [ClientRpc]
    internal void StartExitingAnimationClientRpc()
    {
        // Don't reset the animation state on the client that is interacting, as ServerRpc already did this.
        if (isHealing)
            return;
        SetAnimationState(AnimationState.Exiting);
        healAudio.loop = false;
    }

    private void DoExitingAnimationTick()
    {
        if (!flags.HasFlag(AnimationFlags.StartedSpecialAnimation))
        {
            healingStationAnimator.ResetTrigger("heal");
            playerInteracting.playerBodyAnimator.speed = 1;
            flags |= AnimationFlags.StartedSpecialAnimation;
        }

        if (Time.time - stateStartTime < 1)
            return;
        triggerScript.StopSpecialAnimation();
        SetAnimationState(AnimationState.None);
    }
}
