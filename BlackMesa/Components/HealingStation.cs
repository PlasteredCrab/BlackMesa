using GameNetcodeStuff;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components;

public class HealingStation : NetworkBehaviour
{
    /*public InteractTrigger triggerScript;

    public Animator healingStationAnimator;

    private Coroutine healPlayerCoroutine;

    public AudioSource healAudio;

    private float updateInterval;

    public void HealPlayer()
    {
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

        // Check if the player needs healing and is interacting with the station
        if (localPlayer != null && localPlayer.health < localPlayer.maxHealth)
        {
            PlayHealPlayerEffectServerRpc((int)localPlayer.playerClientId);
            if (healPlayerCoroutine != null)
            {
                StopCoroutine(healPlayerCoroutine);
            }
            healPlayerCoroutine = StartCoroutine(HealPlayerDelayed(localPlayer));
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (updateInterval > 1f)
        {
            updateInterval = 0f;

            if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null)
            {
                // Check if the player can interact (i.e. the player has less than full health)
                triggerScript.interactable = GameNetworkManager.Instance.localPlayerController.health < GameNetworkManager.Instance.localPlayerController.maxHealth;
            }
        }
        else
        {
            updateInterval += Time.deltaTime;
        }
    }

    private IEnumerator HealPlayerDelayed(PlayerController playerToHeal)
    {
        healAudio.Play(); // Play healing audio effect
        yield return new WaitForSeconds(0.75f); // Delay for the heal effect

        healingStationAnimator.SetTrigger("heal"); // Trigger healing animation

        // Check if the player is still in range and needs healing
        if (playerToHeal != null)
        {
            playerToHeal.Heal(25); // Heal the player by 25 health points or any amount you'd like
            playerToHeal.SyncHealthServerRpc(playerToHeal.health); // Sync the player's health across the network
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayHealPlayerEffectServerRpc(int playerHealing)
    {
        PlayHealPlayerEffectClientRpc(playerHealing);
    }

    [ClientRpc]
    public void PlayHealPlayerEffectClientRpc(int playerHealing)
    {
        // If this player isn't the one currently being healed, play the heal effect on their end too
        if (GameNetworkManager.Instance.localPlayerController != null && (int)GameNetworkManager.Instance.localPlayerController.playerClientId != playerHealing)
        {
            if (healPlayerCoroutine != null)
            {
                StopCoroutine(healPlayerCoroutine);
            }
            healPlayerCoroutine = StartCoroutine(HealPlayerDelayed(null)); // Simulate the healing effect on other clients
        }
    }*/
}

