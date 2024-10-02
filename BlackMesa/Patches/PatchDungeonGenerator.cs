using DunGen;
using HarmonyLib;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(DungeonGenerator))]
internal class PatchDungeonGenerator
{
    private static int? originalGenerationAttempts;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(DungeonGenerator.Generate))]
    private static void GenerateNewFloorPrefix(DungeonGenerator __instance)
    {
        if (__instance.DungeonFlow != BlackMesaInterior.BlackMesaFlow)
            return;

        originalGenerationAttempts = __instance.MaxAttemptCount;
        __instance.MaxAttemptCount *= 5;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DungeonGenerator.Generate))]
    private static void GenerateNewFloorPostfix(DungeonGenerator __instance)
    {
        if (!originalGenerationAttempts.HasValue)
            return;

        __instance.MaxAttemptCount = originalGenerationAttempts.Value;
        originalGenerationAttempts = null;
    }
}
