using GameNetcodeStuff;
using System;
using Unity.Netcode;
using UnityEngine;

public class HealingStation : NetworkBehaviour
{
    public InteractTrigger triggerScript;  // Reference to the InteractTrigger
    public AudioSource healAudio;          // Healing audio (2 seconds)
    public Animator healingStationAnimator; // Animator for healing effects

    public float maxHealingCapacity = 100f; // Maximum healing capacity for the machine
    private float currentHealingCapacity;   // Tracks the remaining healing capacity

    private bool isHealing = false;         // Is the healing in progress
    private bool isHolding = false;         // Is the player holding the interaction button
    private float healingStartTime;         // Time when healing starts

    private float lastHealthUpdate = 0f;    // Track health update time
    private float updateInterval = 0.1f;    // Update interval for health UI
    private float healthPerSecond = 10f;    // Healing rate
    private float healthRemainder = 0f;     // Fractional health remainder

    private PlayerControllerB player;       // The player being healed

    private void Start()
    {
        // Initialize the healing capacity
        currentHealingCapacity = maxHealingCapacity;
    }

    // Called while the player is holding the interaction button
    public void OnHoldingInteract()
    {
        // Check if the player is the owner and the interaction button is being held
        if (!isHolding)
        {
            isHolding = true;
            OnPlayerInteract();
        }
    }

    // Called when the player stops holding the interaction button
    public void OnStopHoldingInteract()
    {
        // Stop healing if the player releases the button
        isHolding = false;
        isHealing = false;
        healAudio.Stop();  // Stop the healing audio
        Debug.Log("Healing interrupted by player.");
    }

    public void OnPlayerInteract()
    {
        // Get the local player controller
        player = GameNetworkManager.Instance.localPlayerController;

        // Check if the machine still has capacity and if the player needs healing
        if (currentHealingCapacity > 0 && player != null && player.health < 100)
        {
            StartHealingPlayer();
        }
        else
        {
            Debug.Log("Healing station is out of capacity or player does not need healing.");
        }
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
        if (!isHealing || !isHolding || player == null) return;

        // Calculate healing amount based on deltaTime and healthPerSecond
        float healAmount = healthPerSecond * Time.deltaTime + healthRemainder;

        // Ensure we don't heal more than the remaining capacity
        if (healAmount > currentHealingCapacity)
        {
            healAmount = currentHealingCapacity;  // Limit healing to available capacity
        }

        healthRemainder = healAmount % 1; // Save the fractional remainder
        player.health += (int)healAmount; // Add only the integer portion to health
        currentHealingCapacity -= (int)healAmount; // Reduce the machine's capacity

        bool healCompleted = false;

        // Cap the player's health at 100
        if (player.health >= 100)
        {
            player.health = 100;
            healCompleted = true;
        }

        // If the player is the owner and healing has completed or the update interval has passed
        if (player.IsOwner && (healCompleted || Time.time - lastHealthUpdate > updateInterval))
        {
            player.DamagePlayerServerRpc(damageNumber: 0, player.health);  // Sync health to server
            HUDManager.Instance.UpdateHealthUI(player.health, hurtPlayer: false);  // Update local health UI
            lastHealthUpdate = Time.time;  // Reset last update time
        }

        // End the healing process after 2 seconds or when healing is complete
        if (Time.time - healingStartTime >= 2f || healCompleted)
        {
            isHealing = false;  // End the healing process
            healAudio.Stop();   // Stop the healing audio
        }

        // Check if the machine is out of healing capacity
        if (currentHealingCapacity <= 0)
        {
            currentHealingCapacity = 0;  // Ensure capacity doesn't go negative
            Debug.Log("Healing station is out of healing capacity.");
        }
    }
}
