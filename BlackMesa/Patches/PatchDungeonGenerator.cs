using System.Collections.Generic;
using DunGen;
using HarmonyLib;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(DungeonGenerator))]
internal class PatchDungeonGenerator
{
    private static Dictionary<DungeonGenerator, int> originalGenerationAttempts = [];

    private static bool IsGenerating(GenerationStatus status)
    {
        return status switch
        {
            GenerationStatus.NotStarted => false,
            GenerationStatus.Complete => false,
            GenerationStatus.Failed => false,
            _ => true,
        };
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(DungeonGenerator.ChangeStatus))]
    private static void GenerateNewFloorPrefix(DungeonGenerator __instance, GenerationStatus status)
    {
        if (IsGenerating(status))
        {
            if (originalGenerationAttempts.ContainsKey(__instance))
                return;
            if (!BlackMesaInterior.IsBlackMesaInterior(__instance.DungeonFlow))
                return;

            originalGenerationAttempts[__instance] = __instance.MaxAttemptCount;
            __instance.MaxAttemptCount *= 5;
            return;
        }

        if (originalGenerationAttempts.Remove(__instance, out var attempts))
            __instance.MaxAttemptCount = attempts;
    }
}
