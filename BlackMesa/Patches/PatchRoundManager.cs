using HarmonyLib;
using System.Collections.Generic;
using Unity.Netcode;

namespace BlackMesa.Patches
{
    internal static class PatchRoundManager
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnSyncedProps))]
        private static void SpawnSyncedPropsPostfix(RoundManager __instance)
        {
            if (__instance.dungeonGenerator?.Generator.DungeonFlow != BlackMesaInterior.BlackMesaFlow)
                return;

            var spawnedObjects = __instance.spawnedSyncedObjects;

            var scrapReferences = new List<NetworkObjectReference>();
            var scrapValues = new List<int>();

            foreach (var spawnedObject in spawnedObjects)
            {
                if (spawnedObject.GetComponent<GrabbableObject>() is not { } item)
                    continue;
                // If the item doesn't have a scan node, setting the value will complain.
                if (spawnedObject.GetComponentInChildren<ScanNodeProperties>() == null)
                    continue;

                if (item.itemProperties.isScrap && item.scrapValue == 0)
                {
                    var value = (int)(__instance.AnomalyRandom.Next(item.itemProperties.minValue, item.itemProperties.maxValue) * __instance.scrapValueMultiplier);
                    scrapReferences.Add(item.NetworkObject);
                    scrapValues.Add(value);
                }
            }

            __instance.SyncScrapValuesClientRpc([.. scrapReferences], [.. scrapValues]);
        }
    }
}
