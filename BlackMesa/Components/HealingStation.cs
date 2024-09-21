using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class HealingStation : NetworkBehaviour
{
    public InteractTrigger triggerScript;  // Reference to the InteractTrigger
    public AudioSource healAudio;          // Healing audio (2 seconds)
    public Animator healingStationAnimator; // Animator for healing effects

    private bool isHealing = false;        // Is the healing in progress
    private float healingStartTime;        // Time when healing starts

    private float lastHealthUpdate = 0f;   // Track health update time
    private float updateInterval = 0.1f;   // Update interval for health UI
    private float healthPerSecond = 10f;   // Healing rate
    private float healthRemainder = 0f;    // Fractional health remainder

    private PlayerControllerB player;       // The player being healed

    private void Start()
    {
        // Ensure the trigger calls healing function
        triggerScript.onInteract.AddListener(OnPlayerInteract);
    }

    public void OnPlayerInteract(PlayerControllerB playerToHeal)
    {
        // Start healing if the player is not at full health
        if (playerToHeal != null && playerToHeal.health < 100)
        {
            player = playerToHeal;
            StartHealingPlayer();
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
        if (!isHealing || player == null) return;

        float healAmount = healthPerSecond * Time.deltaTime + healthRemainder;
        healthRemainder = healAmount % 1;
        player.health += (int)healAmount;

        bool healCompleted = false;

        if (player.health >= 100)
        {
            player.health = 100;
            healCompleted = true;
        }

        if (player.IsOwner && (healCompleted || Time.time - lastHealthUpdate > updateInterval))
        {
            player.DamagePlayerServerRpc(damageNumber: 0, player.health);
            HUDManager.Instance.UpdateHealthUI(player.health, hurtPlayer: false);
            lastHealthUpdate = Time.time;
        }

        if (Time.time - healingStartTime >= 2f || healCompleted)
        {
            isHealing = false;  // End the healing process after 2 seconds
        }
    }
}
