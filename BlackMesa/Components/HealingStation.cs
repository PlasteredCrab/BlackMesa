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

    public InteractTrigger triggerScript;

    public AudioSource healAudio;
    public Animator healingStationAnimator;

    public int maxHealingCapacity = 100;
    private int remainingHealingCapacity;

    private int maximumPlayerHealth;

    private bool isHealing = false;

    private PlayerControllerB playerInteracting;
    private AnimationState state = AnimationState.None;
    private float stateStartTime;

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
    }

    // Handles the InteractTrigger.onInteractEarly event
    public void StartHealingLocalPlayer()
    {
        isHealing = true;
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
        healAudio.Play();
        healingStationAnimator.SetTrigger("heal");
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

        if (playerInteracting.health > originalHealth)
        {
            HUDManager.Instance.UpdateHealthUI(playerInteracting.health, hurtPlayer: false);
            if (playerInteracting.health >= 10)
                playerInteracting.MakeCriticallyInjured(false);
        }

        remainingHealingCapacity -= playerInteracting.health - originalHealth;
        if (remainingHealingCapacity <= 0)
        {
            remainingHealingCapacity = 0;
            healEnded = true;
        }

        // Ensure that all clients see the health modification on an interval and after healing ends.
        if (healEnded || Time.time - lastServerHealthUpdate > serverHealthUpdateInterval)
        {
            UpdatePlayerHealthAndCapacity();
            lastServerHealthUpdate = Time.time;
        }

        if (healEnded)
        {
            isHealing = false;
            healAudio.Stop();
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
        UpdatePlayerHealthClientRpc(playerID, health, capacity);
    }

    [ClientRpc]
    private void UpdatePlayerHealthClientRpc(int playerID, int health, int capacity)
    {
        if (isHealing)
            return;

        var player = StartOfRound.Instance.allPlayerScripts[playerID];
        player.DamageOnOtherClients(0, health);

        remainingHealingCapacity = Math.Min(capacity, remainingHealingCapacity);
    }

    private void DoHealingAnimationTick()
    {
        // Stop the animation after 1 second to hold the charging animation in the middle.
        if (Time.time - stateStartTime < 1)
            return;
        if (playerInteracting.playerBodyAnimator.speed != 0)
        {
            playerInteracting.playerBodyAnimator.speed = 0;
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
    }

    private void DoExitingAnimationTick()
    {
        if (playerInteracting.playerBodyAnimator.speed != 1)
        {
            playerInteracting.playerBodyAnimator.speed = 1;
        }

        if (Time.time - stateStartTime < 1)
            return;
        triggerScript.StopSpecialAnimation();
        SetAnimationState(AnimationState.None);
    }
}
