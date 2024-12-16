using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;

namespace BlackMesa.Components;

public class PushRegion : MonoBehaviour
{
    public PushRegion oppositePushRegion;

    private Vector3 lastPosition;
    private List<PlayerControllerB> playersToPush = [];

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        // Scuffed workaround for the fact that killed players teleport to the hidden
        // players location without triggering OnTriggerExit().
        playersToPush.Clear();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerControllerB player))
            playersToPush.Add(player);
    }

    private void LateUpdate()
    {
        var position = transform.position;
        var delta = position - lastPosition;

        if (delta == Vector3.zero)
            return;

        var playerIndex = 0;
        while (playerIndex < playersToPush.Count)
        {
            var player = playersToPush[playerIndex];
            player.transform.position += delta;
            if (oppositePushRegion != null && oppositePushRegion.playersToPush.Contains(player))
            {
                player.KillPlayer(Vector3.zero, true, CauseOfDeath.Crushing);
                playersToPush.RemoveAt(playerIndex);
            }
            else
            {
                playerIndex++;
            }
        }

        lastPosition = position;
    }
}
