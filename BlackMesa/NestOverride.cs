using Unity.Netcode;
using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;

namespace BlackMesa
{
    public class NestOverride : MonoBehaviour
    {
        internal static NestOverride Instance;

        [SerializeField]
        public List<Transform> NestPositions;

        private void Awake()
        {
            Instance = this;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnNestObjectForOutsideEnemy))]
        private static bool SpawnNestObjectForOutsideEnemyPrefix(RoundManager __instance, EnemyType enemyType, System.Random randomSeed)
        {
            if (__instance.currentLevel.name != "Black Mesa")
                return true;

            int nestIndex = randomSeed.Next(0, Instance.NestPositions.Count);
            Vector3 position = Instance.NestPositions[nestIndex].position;

            GameObject gameObject = Instantiate(enemyType.nestSpawnPrefab, position, Quaternion.Euler(Vector3.zero));
            gameObject.transform.Rotate(Vector3.up, randomSeed.Next(-180, 180), Space.World);
            if (!gameObject.gameObject.GetComponentInChildren<NetworkObject>())
                Debug.LogError("Error: No NetworkObject found in enemy nest spawn prefab that was just spawned on the host: '" + gameObject.name + "'");
            else
                gameObject.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
            if (!gameObject.GetComponent<EnemyAINestSpawnObject>())
                Debug.LogError("Error: No EnemyAINestSpawnObject component in nest object prefab that was just spawned on the host: '" + gameObject.name + "'");
            else
                __instance.enemyNestSpawnObjects.Add(gameObject.GetComponent<EnemyAINestSpawnObject>());
            enemyType.nestsSpawned++;

            return false;
        }
    }
}
