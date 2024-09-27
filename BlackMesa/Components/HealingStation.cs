using GameNetcodeStuff;
using System;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components;

public class HealingStation : StationBase
{
    public float healthSyncInterval;
    public float healthPerSecond = 10f;

    private int maximumPlayerHealth;

    private float healthRemainder = 0f;

    private float lastHealthSync = 0f;

    protected override void Start()
    {
        base.Start();

        maximumPlayerHealth = GameNetworkManager.Instance.localPlayerController.health;

        if (healthSyncInterval == 0)
            healthSyncInterval = capacitySyncInterval;
    }

    protected override void UpdateInteractabilityWithCapacity()
    {
        if (GameNetworkManager.Instance.localPlayerController.health >= maximumPlayerHealth)
        {
            triggerScript.disabledHoverTip = "Already full health";
            triggerScript.interactable = false;
            return;
        }

        triggerScript.interactable = true;
    }

    protected override void OnActivatedByPlayer(PlayerControllerB player, ref float capacityInterpolationDelay)
    {
        if (isActiveOnLocalClient)
            capacityInterpolationDelay = 1 / healthPerSecond;

        healthRemainder = 0;
    }

    protected override TickResult DoActiveTick()
    {
        TickResult result = TickResult.Continue;

        // Calculate healing amount based on deltaTime and healthPerSecond.
        float healAmount = healthPerSecond * Time.deltaTime + healthRemainder;
        healAmount = Math.Min(healAmount, remainingCapacity);

        // Add the integer portion of the healing amount and save the remainder.
        var originalHealth = playerInteracting.health;
        healthRemainder = healAmount % 1;
        playerInteracting.health += (int)healAmount;

        // Cap the player's health at 100.
        if (playerInteracting.health >= maximumPlayerHealth)
        {
            playerInteracting.health = maximumPlayerHealth;
            result = TickResult.Deactivate;
        }

        var healthDelta = playerInteracting.health - originalHealth;

        if (healthDelta > 0)
        {
            HUDManager.Instance.UpdateHealthUI(playerInteracting.health, hurtPlayer: false);
            if (playerInteracting.health >= 10)
                playerInteracting.MakeCriticallyInjured(false);

            var timeSinceLastHealthUpdate = Time.time - lastHealthSync;
            if (timeSinceLastHealthUpdate >= healthSyncInterval)
                UpdatePlayerHealth();
        }

        ConsumeCapacity(healthDelta);

        return result;
    }

    private void UpdatePlayerHealth()
    {
        UpdatePlayerHealthServerRpc((int)playerInteracting.playerClientId, playerInteracting.health, playerInteracting.health >= maximumPlayerHealth);
        lastHealthSync = Time.time;
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerHealthServerRpc(int playerID, int health, bool fullyHealed)
    {
        UpdatePlayerHealthClientRpc(playerID, health, fullyHealed);
    }

    [ClientRpc]
    private void UpdatePlayerHealthClientRpc(int playerID, int health, bool fullyHealed)
    {
        if (isActiveOnLocalClient)
            return;

        var player = StartOfRound.Instance.allPlayerScripts[playerID];
        player.DamageOnOtherClients(0, health);
        if (fullyHealed)
            StopActiveAudioOnClient();
    }
}
