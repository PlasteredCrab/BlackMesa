using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalLevelLoader;
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
            var BlackMesaAssets = AssetBundle.LoadFromFile(Path.Combine(directoryName, "blackmesainterior"));
            if (BlackMesaAssets == null)
            {
                Logger.LogError("Failed to load Interior Dungeon assets.");
                return;
            }
            Logger.LogInfo("Interior Assets loaded successfully.");

            // Retrieve the Extended Dungeon Flow from the AssetBundle.
            ExtendedDungeonFlow BlackMesaExtendedDungeon = BlackMesaAssets.LoadAsset<ExtendedDungeonFlow>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Black Mesa Extended Flow.asset");
            if (BlackMesaExtendedDungeon == null)
            {
                Logger.LogError("Failed to load Interior Dungeon Flow.");
                return;
            }

            // Register the Extended Dungeon Flow with LLL.
            PatchedContent.RegisterExtendedDungeonFlow(BlackMesaExtendedDungeon);
            Logger.LogInfo("Loaded Extended DungeonFlow.");

            // Apply patches.
            harmony.PatchAll(typeof(PatchStartOfRound));
        }

        // variables that are called throughout the script

        // Harmony instance used for patching methods in the game
        private readonly Harmony harmony = new(GUID);

        // Singleton instance of the BlackMesaInterior class
        public static BlackMesaInterior Instance;

        // Logger instance for logging messages and debugging information  
        new internal static ManualLogSource Logger;    
    }

}
