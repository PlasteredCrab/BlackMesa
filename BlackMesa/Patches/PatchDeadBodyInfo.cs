using BlackMesa.Components;
using HarmonyLib;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(DeadBodyInfo))]
internal static class PatchDeadBodyInfo
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(DeadBodyInfo.SetRagdollPositionSafely))]
    private static void SetRagdollPositionSafelyPostfix(DeadBodyInfo __instance)
    {
        Barnacle.OnPlayerTeleported(__instance.playerScript);
    }
}
