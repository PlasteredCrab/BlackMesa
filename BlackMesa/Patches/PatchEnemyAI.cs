using HarmonyLib;
using System.Collections.Generic;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(EnemyAI))]
internal static class PatchEnemyAI
{
    internal static HashSet<EnemyAI> AllEnemies = [];

    [HarmonyPostfix]
    [HarmonyPatch(nameof(EnemyAI.Start))]
    private static void StartPostfix(EnemyAI __instance)
    {
        AllEnemies.Add(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(EnemyAI.OnDestroy))]
    private static void OnDestroyPostfix(EnemyAI __instance)
    {
        AllEnemies.Remove(__instance);
    }
}
