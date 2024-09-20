using HarmonyLib;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Patches;

internal static class PatchNetworkManager
{
    private static List<GameObject> networkPrefabs = [];

    internal static void AddNetworkPrefab(GameObject prefab)
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.AddNetworkPrefab(prefab);
            return;
        }

        networkPrefabs.Add(prefab);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NetworkConfig), nameof(NetworkConfig.InitializePrefabs))]
    private static void InitializePrefabsPostfix(NetworkConfig __instance)
    {
        var prefabs = __instance.Prefabs;
        foreach (var prefab in networkPrefabs)
        {
            var container = new NetworkPrefab()
            {
                Prefab = prefab,
            };
            prefabs.Add(container);
        }
        networkPrefabs = [];
    }
}
