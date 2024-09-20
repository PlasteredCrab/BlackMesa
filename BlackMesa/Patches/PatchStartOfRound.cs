using HarmonyLib;
using UnityEngine;

namespace BlackMesa.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    public sealed class PatchStartOfRound
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(StartOfRound.ShipHasLeft))]
        private static void ShipHasLeftPostFix()
        {
            var handheldTVs = Object.FindObjectsByType<HandheldTVCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var handheldTV in handheldTVs)
                handheldTV.DestroyTv();
        }
    }
}
