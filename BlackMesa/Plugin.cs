using BepInEx;
using BepInEx.Logging;
using BlackMesa.Components;
using BlackMesa.Patches;
using DunGen.Graph;
using HarmonyLib;
using LethalLevelLoader;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BlackMesa
{

    [BepInPlugin(GUID, NAME, VERSION)]
    public class BlackMesaInterior : BaseUnityPlugin
    {
        public const string GUID = "Plastered_Crab.BlackMesaInterior";
        public const string NAME = "Black Mesa Interior";
        public const string VERSION = "1.1.0";

        // Awake method is called before the Menu Screen initialization
        private void Awake()
        {
            // Store the plugin as a singleton instance.
            Instance = this;

            // Store a logger in a static field for use throughout the mod.
            Logger = base.Logger;

            // Load Interior Dungeon assets from the AssetBundle.
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var blackMesaAssets = AssetBundle.LoadFromFile(Path.Combine(directoryName, "blackmesainterior"));
            if (blackMesaAssets == null)
            {
                Logger.LogError("Failed to load Interior Dungeon assets.");
                return;
            }
            Logger.LogInfo("Interior Assets loaded successfully.");

            // Retrieve the Extended Dungeon Flow from the AssetBundle.
            ExtendedDungeonFlow blackMesaExtendedDungeon = blackMesaAssets.LoadAsset<ExtendedDungeonFlow>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Black Mesa Extended Flow.asset");
            if (blackMesaExtendedDungeon == null)
            {
                Logger.LogError("Failed to load Interior Dungeon Flow.");
                return;
            }

            BlackMesaFlow = blackMesaExtendedDungeon.DungeonFlow;

            // Register the Extended Dungeon Flow with LLL.
            PatchedContent.RegisterExtendedDungeonFlow(blackMesaExtendedDungeon);
            Logger.LogInfo("Loaded Extended DungeonFlow.");

            // Apply patches.
            harmony.PatchAll(typeof(PatchStartOfRound));
            harmony.PatchAll(typeof(PatchRoundManager));
            harmony.PatchAll(typeof(PatchNetworkManager));

            RegisterNetworkBehaviour(typeof(Tripmine), blackMesaAssets.LoadAsset<GameObject>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Prefabs/Props/Tripmine.prefab"));
        }

        // variables that are called throughout the script

        // Harmony instance used for patching methods in the game
        private readonly Harmony harmony = new(GUID);

        // Singleton instance of the BlackMesaInterior class
        public static BlackMesaInterior Instance;

        // Logger instance for logging messages and debugging information  
        new internal static ManualLogSource Logger;

        internal static DungeonFlow BlackMesaFlow;

        private static void RegisterNetworkBehaviour(Type type, GameObject prefab)
        {
            type.GetMethod("InitializeRPCS_" + type.Name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            PatchNetworkManager.AddNetworkPrefab(prefab);
        }
    }

}
