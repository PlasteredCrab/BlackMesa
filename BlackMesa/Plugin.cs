using BepInEx;
using BepInEx.Logging;
using BlackMesa.Patches;
using BlackMesa.Scriptables;
using DunGen;
using DunGen.Graph;
using HarmonyLib;
using PathfindingLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BlackMesa
{

    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("imabatby.lethallevelloader")]
    [BepInDependency(PathfindingLibPlugin.PluginGUID, "2.0.0")]
    public class BlackMesaInterior : BaseUnityPlugin
    {
        public const string GUID = "Plastered_Crab.BlackMesaInterior";
        public const string NAME = "Black Mesa Interior";
        public const string VERSION = "3.2.3";

        public static BlackMesaInterior Instance;

        private readonly Harmony harmony = new(GUID);

        new internal static ManualLogSource Logger;

        internal static AssetBundle Bundle;

        internal static GameObject GenerationRulesPrefab;

        internal static List<GameObject> PrefabsWithAudioSources = [];

        // Awake method is called before the Menu Screen initialization
        private void Awake()
        {
            // Store the plugin as a singleton instance.
            Instance = this;

            // Store a logger in a static field for use throughout the mod.
            Logger = base.Logger;

            // Apply patches.
            harmony.PatchAll(typeof(PatchStartOfRound));
            harmony.PatchAll(typeof(PatchRoundManager));
            harmony.PatchAll(typeof(PatchNetworkManager));
            harmony.PatchAll(typeof(PatchDungeonGenerator));
            harmony.PatchAll(typeof(PatchLungProp));
            harmony.PatchAll(typeof(PatchPlayerControllerB));
            harmony.PatchAll(typeof(PatchDeadBodyInfo));
            harmony.PatchAll(typeof(PatchMenuManager));
            harmony.PatchAll(typeof(PatchAnimator));
            harmony.PatchAll(typeof(PatchEnemyAI));

            string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Bundle = AssetBundle.LoadFromFile(Path.Combine(assemblyPath, "Assets", "blackmesascriptassets"));
            GenerationRulesPrefab = Bundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/GenerationRules.prefab");
        }

        internal static bool IsBlackMesaInterior(DungeonFlow flow)
        {
            return flow.name == "Black Mesa";
        }

        internal static void BatchAllTileModels(Dungeon dungeon)
        {
            int batchedTiles = 0;

            foreach (var tile in dungeon.AllTiles)
            {
                if (tile == null)
                    continue;
                var modelsChild = tile.transform.Find("Models");
                if (modelsChild == null)
                    continue;
                StaticBatchingUtility.Combine(modelsChild.gameObject);
                batchedTiles++;
            }

            Logger.LogInfo($"Marked {batchedTiles} tiles to be static-batched.");
        }
    }

}
