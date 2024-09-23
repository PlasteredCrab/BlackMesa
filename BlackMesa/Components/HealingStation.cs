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

        ((IHittable)GameNetworkManager.Instance.localPlayerController).Hit(4, Vector3.zero, GameNetworkManager.Instance.localPlayerController);
        ((IHittable)GameNetworkManager.Instance.localPlayerController).Hit(4, Vector3.zero, GameNetworkManager.Instance.localPlayerController);
        ((IHittable)GameNetworkManager.Instance.localPlayerController).Hit(4, Vector3.zero, GameNetworkManager.Instance.localPlayerController);
    }

    private void SetAnimationState(AnimationState state)
    {
        this.state = state;
        stateStartTime = Time.time;
    }

    public void StartHealingLocalPlayer()
    {
        isHealing = true;
        StartAnimation(GameNetworkManager.Instance.localPlayerController);
    }

    public void StartAnimation(PlayerControllerB player)
    {
        playerInteracting = player;
        SetAnimationState(AnimationState.Healing);

        healAudio.Play();
        healingStationAnimator.SetTrigger("heal");
        BlackMesaInterior.Logger.LogInfo($"Started healing animation on {player}");
    }

    private void Update()
    {
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
        if (healEnded)
            BlackMesaInterior.Logger.LogInfo($"Heal interact stopped");

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
        if (remainingHealingCapacity < 0)
            remainingHealingCapacity = 0;

        // Ensure that all clients see the health modification on an interval and after healing ends.
        if (healEnded || Time.time - lastServerHealthUpdate > serverHealthUpdateInterval)
        {
            playerInteracting.DamagePlayerServerRpc(damageNumber: 0, playerInteracting.health);
            lastServerHealthUpdate = Time.time;
        }

        if (healEnded)
        {
            isHealing = false;
            healAudio.Stop();
            BlackMesaInterior.Logger.LogInfo("Heal ended");
            StartExitingAnimationServerRpc();
        }
    }

    private void DoHealingAnimationTick()
    {
        // Stop the animation after 1 second to hold the charging animation in the middle.
        if (Time.time - stateStartTime < 1)
            return;
        if (playerInteracting.playerBodyAnimator.speed != 0)
        {
            BlackMesaInterior.Logger.LogInfo("Animation stopped");
            playerInteracting.playerBodyAnimator.speed = 0;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    internal void StartExitingAnimationServerRpc()
    {
        BlackMesaInterior.Logger.LogInfo("StartExitingAnimationServerRpc");
        StartExitingAnimationClientRpc();
    }

    [ClientRpc]
    internal void StartExitingAnimationClientRpc()
    {
        BlackMesaInterior.Logger.LogInfo("StartExitingAnimationClientRpc");
        SetAnimationState(AnimationState.Exiting);
    }

    private void DoExitingAnimationTick()
    {
        if (playerInteracting.playerBodyAnimator.speed != 1)
        {
            playerInteracting.playerBodyAnimator.speed = 1;
            BlackMesaInterior.Logger.LogInfo("Animation resumed");
        }

        if (Time.time - stateStartTime < 1)
            return;
        triggerScript.StopSpecialAnimation();
        BlackMesaInterior.Logger.LogInfo($"Stopped special animation");
        SetAnimationState(AnimationState.None);
    }
}
