using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;

namespace BlackMesa.Components;

public class PushRegion : MonoBehaviour
{
    public PushRegion oppositePushRegion;

    private Vector3 lastPosition;
    private HashSet<PlayerControllerB> playersToPush = [];

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerControllerB player))
            playersToPush.Add(player);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerControllerB player))
            playersToPush.Remove(player);
    }

    private void LateUpdate()
    {
        var position = transform.position;
        var delta = position - lastPosition;

        if (delta == Vector3.zero)
            return;

        foreach (var playerToPush in playersToPush)
        {
            playerToPush.transform.position += delta;
            if (oppositePushRegion != null && oppositePushRegion.playersToPush.Contains(playerToPush))
            {
                Debug.Log($"{playerToPush} is being pushed by two opposite push regions, killing.");
                playerToPush.KillPlayer(Vector3.zero, true, CauseOfDeath.Crushing);
            }
        }

        lastPosition = position;
    }
}
