using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

public class HealingStation : NetworkBehaviour
{
    public InteractTrigger triggerScript;
    public AudioSource healAudio;
    public Animator healingStationAnimator;

    private bool isHealing = false;
    private float healingStartTime;

    private float lastHealthUpdate = 0f;
    private float updateInterval = 0.1f;
    private float healthPerSecond = 10f;
    private float healthRemainder = 0f;

    public void OnPlayerInteract()
    {
        if (GameNetworkManager.Instance.localPlayerController.health < 100)
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

        var player = GameNetworkManager.Instance.localPlayerController;

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
