using GameNetcodeStuff;
using System;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components;

public class HealingStation : NetworkBehaviour
{
    public InteractTrigger triggerScript;

    public AudioSource healAudio;
    public Animator healingStationAnimator;

    public int maxHealingCapacity = 100;
    private int remainingHealingCapacity;

    private PlayerControllerB localPlayer;
    private int maximumPlayerHealth;

    private bool isHealing = false;
    private float healingStartTime;

    private float healthPerSecond = 10f;
    private float healthRemainder = 0f;

    private float lastServerHealthUpdate = 0f;
    private float serverHealthUpdateInterval = 0.25f;

    private void Start()
    {
        remainingHealingCapacity = maxHealingCapacity;

        localPlayer = GameNetworkManager.Instance.localPlayerController;
        maximumPlayerHealth = localPlayer.health;

        triggerScript.onInteractEarly.RemoveAllListeners();
        triggerScript.onInteract.RemoveAllListeners();
        triggerScript.onStopInteract.RemoveAllListeners();
        triggerScript.onCancelAnimation.RemoveAllListeners();

        triggerScript.onInteractEarly.AddListener(OnPlayerInteract);

        ((IHittable)GameNetworkManager.Instance.localPlayerController).Hit(4, Vector3.zero, GameNetworkManager.Instance.localPlayerController);
    }

    public void OnPlayerInteract(PlayerControllerB _)
    {
        // Check if the machine still has capacity and if the player needs healing
        if (remainingHealingCapacity > 0 && localPlayer.health < maximumPlayerHealth)
            StartHealingPlayer();
    }

    // Initiate healing sequence
    public void StartHealingPlayer()
    {
        healingStartTime = Time.time;
        isHealing = true;

        healAudio.Play();  // Play healing audio (2 seconds)
        healingStationAnimator.SetTrigger("heal"); // Start healing animation
    }

    private void Update()
    {
        if (!isHealing)
            return;

        // Calculate healing amount based on deltaTime and healthPerSecond.
        float healAmount = healthPerSecond * Time.deltaTime + healthRemainder;
        healAmount = Math.Min(healAmount, remainingHealingCapacity);

        bool healCompleted = false;
        var originalHealth = localPlayer.health;

        // Add the integer portion of the healing amount and save the remainder.
        healthRemainder = healAmount % 1;
        localPlayer.health += (int)healAmount;

        // Cap the player's health at 100.
        if (localPlayer.health >= maximumPlayerHealth)
        {
            localPlayer.health = maximumPlayerHealth;
            healCompleted = true;
        }

        HUDManager.Instance.UpdateHealthUI(localPlayer.health, hurtPlayer: false);

        remainingHealingCapacity -= localPlayer.health - originalHealth;
        if (remainingHealingCapacity < 0)
            remainingHealingCapacity = 0;

        // Ensure that all clients see the health modification on an interval and after healing ends.
        if (healCompleted || Time.time - lastServerHealthUpdate > serverHealthUpdateInterval)
        {
            localPlayer.DamagePlayerServerRpc(damageNumber: 0, localPlayer.health);
            lastServerHealthUpdate = Time.time;
        }

        if (Time.time - healingStartTime >= 2f || healCompleted)
        {
            isHealing = false;
            healAudio.Stop();
        }
    }
}
