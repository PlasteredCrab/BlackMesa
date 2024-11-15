using BlackMesa.Components;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using UnityEngine;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
internal class PatchPlayerControllerB
{
    internal static bool[] playerLockedPositions = [];

    private static void AllocatePlayersArrays()
    {
        if (playerLockedPositions.Length != StartOfRound.Instance.allPlayerScripts.Length)
        {
            var startIndex = playerLockedPositions.Length;
            Array.Resize(ref playerLockedPositions, StartOfRound.Instance.allPlayerScripts.Length);
            for (var i = startIndex; i < playerLockedPositions.Length; i++)
                playerLockedPositions[i] = false;
        }
    }

    internal static void SetPlayerPositionLocked(PlayerControllerB player, bool locked)
    {
        AllocatePlayersArrays();

        if ((int)player.playerClientId < playerLockedPositions.Length)
            playerLockedPositions[player.playerClientId] = locked;

        player.disableMoveInput = locked;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerControllerB.LateUpdate))]
    private static void LateUpdatePrefix(PlayerControllerB __instance)
    {
        if ((int)__instance.playerClientId < playerLockedPositions.Length)
        {
            var locked = playerLockedPositions[__instance.playerClientId];

            if (locked)
            {
                __instance.transform.localPosition = Vector3.zero;

                __instance.fallValue = 0;
                __instance.fallValueUncapped = 0;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(PlayerControllerB.SpawnDeadBody))]
    private static void SpawnDeadBodyPostfix(PlayerControllerB __instance)
    {
        Debug.Log($"Spawn dead body");
        Barnacle.OnRagdollSpawnedForPlayer(__instance);
    }
}
