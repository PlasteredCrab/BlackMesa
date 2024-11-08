using BlackMesa.Components;
using HarmonyLib;
using UnityEngine;

namespace BlackMesa.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    public sealed class PatchStartOfRound
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(StartOfRound.Awake))]
        private static void AwakePostfix()
        {
            Object.Instantiate(BlackMesaInterior.GenerationRulesPrefab);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(StartOfRound.ShipLeave))]
        private static void ShipLeavePostFix()
        {
            var handheldTVs = Object.FindObjectsByType<HandheldTVCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var handheldTV in handheldTVs)
                handheldTV.ShipIsLeaving();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(StartOfRound.ShipHasLeft))]
        private static void ShipHasLeftPostFix()
        {
            var handheldTVs = Object.FindObjectsByType<HandheldTVCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var handheldTV in handheldTVs)
                handheldTV.ExplodeClientRPC();
        }
    }
}
