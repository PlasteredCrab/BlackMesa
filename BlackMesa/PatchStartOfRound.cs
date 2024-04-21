using HarmonyLib;
using UnityEngine;

namespace BlackMesa
{
    [HarmonyPatch(typeof(StartOfRound))]
    public sealed class PatchStartOfRound
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(StartOfRound.ShipHasLeft))]
        private static void ShipHasLeftPostFix()
        {
            Debug.Log("Trying to DespawnProps");
            foreach (var spawnedSyncedObject in RoundManager.Instance.spawnedSyncedObjects)
            {
                Debug.Log($"{spawnedSyncedObject}");
                if (spawnedSyncedObject.TryGetComponent<HandheldTVCamera>(out var handheldTV))
                {
                    handheldTV.DestroyTv();
                    //Object.Destroy(spawnedSyncedObject);
                    //Debug.Log("Trying to delete it");
                }
            }
        }
    }
}
