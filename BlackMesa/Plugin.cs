using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using DunGen;
using DunGen.Graph;
using LethalLib;
using LethalLib.Modules;
using LethalLevelLoader;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Chnage anything that says Your Mod to the Name of Mods name that you are making and or the name of the interior
namespace BlackMesa
{

    [BepInPlugin(GUID, NAME, VERSION)] // Set your mod name and set your version number
    public class BlackMesaInterior : BaseUnityPlugin
    {
        public const string GUID = "Plastered_Crab.BlackMesaInterior";
        public const string NAME = "Black Mesa Interior";
        public const string VERSION = "0.9.0";

        // Awake method is called before the Menu Screen initialization
        private void Awake()
        {
            // Instantiating game objects and managing singleton instance
            Instance = this;

            // Retrieving types from the executing assembly
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                // Invoking methods with RuntimeInitializeOnLoadMethodAttribute
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
                foreach (MethodInfo methodInfo in methods)
                {
                    object[] customAttributes = methodInfo.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    bool flag2 = customAttributes.Length != 0;
                    if (flag2)
                    {
                        methodInfo.Invoke(null, null);
                        mls.LogInfo("Invoked method with RuntimeInitializeOnLoadMethodAttribute");
                    }
                }
            }
            // Creating a logger to handle log errors and debug messages
            mls = BepInEx.Logging.Logger.CreateLogSource("Black Mesa Interior");

            // Loading Interior Dungeon assets from AssetBundle
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            BlackMesaAssets = AssetBundle.LoadFromFile(Path.Combine(directoryName, "blackmesainterior")); // Loading Assetbundle name it your mod and asset name
            if (BlackMesaAssets == null)
            {
                mls.LogError("Failed to load Interior Dungeon assets.");
                return;
            }

            mls.LogInfo("Interior Assets loaded successfully");

            // Loading Dungeon Flow from AssetBundle
            DungeonFlow BlackMesaFlow = BlackMesaAssets.LoadAsset<DungeonFlow>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Black Mesa.asset");// use your file path and dungenflow name
            if (BlackMesaFlow == null)
            {
                mls.LogError("Failed to load Interior Dungeon Flow.");
                return;
            }

            // Configuration setup
            configInteriorRarity = Config.Bind("Black Mesa Interior", "InteriorRarity", 35, new ConfigDescription("The chance that the Interior tileset will be chosen. The higher the value, the higher the chance. By default, the Interior will appear on valid moons with a roughly one in ten chance.", new AcceptableValueRange<int>(0, 99999)));
            configInteriorMoons = Config.Bind("Black Mesa Interior", "InteriorMoons", "list", new ConfigDescription("Use 'list' to specify a custom list of moons for the Interior to appear on. Individual moons can be added to the list by editing the InteriorDungeonMoonsList config entry.", new AcceptableValueList<string>(configInteriorMoonsValues)));
            configInteriorMoonsList = Config.Bind("Black Mesa Interior", "InteriorDungeonMoonsList", "Black Mesa:99999", new ConfigDescription("Note: Requires 'InteriorMoons' to be set to 'list'. \nCan be used to specify a list of moons with individual rarities for moons to spawn on. \nRarity values will override the default rarity value provided in Interior Rarity and will override InteriorGuaranteed. To guarantee dungeon spawning on a moon, assign arbitrarily high rarity value (e.g.  99999). \nMoons and rarities should be provided as a comma-separated list in the following format: 'Name:Rarity' Example: March:150,Offense:150 \nNote: Moon names are checked by string matching, i.e. the moon name 'dine' would enable spawning on 'dine', 'diner' and 'undine'. Be careful with modded moon names.", null));
            configGuaranteedInterior = Config.Bind("Black Mesa Interior", "InteriorGuaranteed", false, new ConfigDescription("If enabled, the Interior will be effectively guaranteed to spawn. Only recommended for debugging/sightseeing purposes.", null)); // unused Config
            configSizeClampingEnabled = Config.Bind("Size", "DungeonSizeClampingEnabled", false, "Enables the smoothed dungeon size clamping functionality. It is recommended to leave this set to false unless the interior fails to generate on certain moons.");
            var dungeonSizeAcceptableValues = new AcceptableValueRange<float>(0.5f, 10.0f);
            configSizeClampingMin = Config.Bind("Size", "DungeonSizeClampingMin", 1f, new ConfigDescription("Input the dungeon's minimum size multiplier.", dungeonSizeAcceptableValues));
            configSizeClampingMax = Config.Bind("Size", "DungeonSizeClampingMax", 3f, new ConfigDescription("Input the dungeon's maximum size multiplier.", dungeonSizeAcceptableValues));
            configSizeClampingStrength = Config.Bind("Size", "DungeonSizeClampingStrength", 1f, new ConfigDescription("Defines the amount of effect the clamping should have on the dungeon size multiplier. Lower values will result in less clamping, whereas higher values will limit the values further.", new AcceptableValueRange<float>(0, 1)));
            configTileSize = Config.Bind("Size", "DungeonTileSize", 2.5f, new ConfigDescription("Input the average size of a tile in the Black Mesa dungeon.", new AcceptableValueRange<float>(0.5f, 10.0f)));

            // Create an ExtendedDungeonFlow object and initialize it with dungeon flow information
            ExtendedDungeonFlow BlackMesaExtendedDungeon = ScriptableObject.CreateInstance<ExtendedDungeonFlow>();
            BlackMesaExtendedDungeon.contentSourceName = "Black Mesa Interior";
            BlackMesaExtendedDungeon.dungeonFlow = BlackMesaFlow; // Dungeon flow value used for accessing data from scriptable object and dungeon flow
            BlackMesaExtendedDungeon.dungeonDefaultRarity = 0;

            // Determine the rarity value based on configuration settings
            int newRarity = (configGuaranteedInterior.Value ? 99999 : configInteriorRarity.Value);
            // Based on configured moon settings, register the interior on different types of moons
            if (configInteriorMoons.Value.ToLowerInvariant() == "all")
            {
                // Register interior on all moons, including modded moons
                BlackMesaExtendedDungeon.manualContentSourceNameReferenceList.Add(new StringWithRarity("Lethal Company", newRarity));
                BlackMesaExtendedDungeon.manualContentSourceNameReferenceList.Add(new StringWithRarity("Custom", newRarity));
                mls.LogInfo("Registered Interior on all Moons, Includes Modded Moons.");
            }
            else if ((configInteriorMoons.Value.ToLowerInvariant() == "list") && (configInteriorMoonsList.Value != null))
            {
                string[] array = configInteriorMoonsList.Value.Split(',');
                foreach (string text in array)
                {
                    StringWithRarity stringWithRarity = ParseMoonString(text, newRarity);
                    if (stringWithRarity != null)
                    {
                        BlackMesaExtendedDungeon.manualPlanetNameReferenceList.Add(stringWithRarity);
                        mls.LogInfo($"Registered Interior on moon name {stringWithRarity.Name} with rarity {stringWithRarity.Rarity}");
                    }
                    else
                    {
                        if (stringWithRarity == null)
                        {
                            // Log the error, but continue processing other moons
                            mls.LogWarning($"No moon Added to list value!");
                        }
                        // Add a new StringWithRarity with the default rarity
                        BlackMesaExtendedDungeon.manualPlanetNameReferenceList.Add(new StringWithRarity(text, newRarity));
                    }
                }
            }
            else
            {
                mls.LogError("Invalid 'InteriorDungeonMoons' config value! ");
            }
            // Register the Extended Dungeon Flow with LLL
            PatchedContent.RegisterExtendedDungeonFlow(BlackMesaExtendedDungeon);
            mls.LogInfo("Loaded Extended DungeonFlow");

            // Configure dungeon size parameters and apply Harmony patches
            BlackMesaExtendedDungeon.IsDynamicDungeonSizeRestrictionEnabled = configSizeClampingEnabled.Value;
            BlackMesaExtendedDungeon.DynamicDungeonSizeMinMax = new Vector2(configSizeClampingMin.Value, configSizeClampingMax.Value);
            BlackMesaExtendedDungeon.DynamicDungeonSizeLerpRate = configSizeClampingStrength.Value;
            BlackMesaExtendedDungeon.MapTileSize = configTileSize.Value;

            harmony.PatchAll(typeof(PatchStartOfRound));
        }
        // variables that are called throughout the script

        private readonly Harmony harmony = new(GUID);
        // Harmony instance used for patching methods in the game

        public static BlackMesaInterior Instance;
        // Singleton instance of the MoreInteriorsDunGen class

        internal ManualLogSource mls;
        // Logger instance for logging messages and debugging information

        public static AssetBundle BlackMesaAssets;
        // AssetBundle containing Bunker Dungeon assets

        private ConfigEntry<int> configInteriorRarity;
        // Configuration entry for specifying the rarity of the Bunker dungeon

        private ConfigEntry<string> configInteriorMoons;
        // Configuration entry for specifying the moons where the Bunker dungeon can appear

        private ConfigEntry<string> configInteriorMoonsList;
        // Configuration entry for specifying a list of moons with individual rarities for spawning the Bunker dungeon

        private ConfigEntry<bool> configGuaranteedInterior;
        // Configuration entry for toggling whether the Bunker dungeon is guaranteed to spawn

        // Dungeon size configuration
        private ConfigEntry<bool> configSizeClampingEnabled;
        private ConfigEntry<float> configSizeClampingMin;
        private ConfigEntry<float> configSizeClampingMax;
        private ConfigEntry<float> configSizeClampingStrength;
        private ConfigEntry<float> configTileSize;

        private string[] configInteriorMoonsValues = ["all", "list"];
        // List of preset values for configBunkerMoons entry

        // Function to parse a string representing a moon and its rarity
        public static StringWithRarity ParseMoonString(string moonString, int newRarity)
        {
            // Check if the input string is null or empty
            if (string.IsNullOrEmpty(moonString))
            {
                return null; // Return null if the input string is empty
            }

            // Split the string into moon name and rarity using ':' as the delimiter
            string[] parts = moonString.Split(':');

            // Check if the split resulted in exactly two parts
            if (parts.Length != 2)
            {
                // If only the moon name is present without a rarity, use the default rarity
                return new StringWithRarity(parts[0], newRarity);
            }

            try
            {
                // Parse the rarity part of the string
                int rarity = int.Parse(parts[1]);

                // Create a new StringWithRarity object with the moon name and parsed rarity
                return new StringWithRarity(parts[0], rarity);
            }
            catch (FormatException)
            {
                // If parsing fails, use the default rarity value
                return new StringWithRarity(parts[0], newRarity);
            }
        }
    }
}
