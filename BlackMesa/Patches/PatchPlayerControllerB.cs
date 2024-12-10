using BlackMesa.Components;
using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
internal class PatchPlayerControllerB
{
    private static HashSet<PlayerControllerB> lockedPlayers = [];

    internal static void SetPlayerPositionLocked(PlayerControllerB player, bool locked)
    {
        if (locked)
            lockedPlayers.Add(player);
        else
            lockedPlayers.Remove(player);

        player.disableMoveInput = locked;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerControllerB.LateUpdate))]
    private static void LateUpdatePrefix(PlayerControllerB __instance)
    {
        if (!lockedPlayers.Contains(__instance))
            return;

        __instance.transform.localPosition = Vector3.zero;

        __instance.fallValue = 0;
        __instance.fallValueUncapped = 0;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(PlayerControllerB.SpawnDeadBody))]
    private static void SpawnDeadBodyPostfix(PlayerControllerB __instance)
    {
        Barnacle.OnRagdollSpawnedForPlayer(__instance);
    }
}
