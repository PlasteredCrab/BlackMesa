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
            configGuaranteedInterior = Config.Bind("Black Mesa Interior", "InteriorGuaranteed", defaultValue: false, new ConfigDescription("If enabled, the Interior will be effectively guaranteed to spawn. Only recommended for debugging/sightseeing purposes.", null)); // unused Config
            configDynamicToggle = Config.Bind("Size", "DynamicScaleToggle", true, new ConfigDescription("Enables the next 3 options. \nATTENTION: READ CAREFULLY HOW IT WORKS. This adjust the dungeon size accordingly to which moon you visit, it is recommended to let it on true\nDefault: true", (AcceptableValueBase)null, Array.Empty<object>()));
            configDynamicValue = Config.Bind("Size", "DynamicScaleValue", 0.9f, new ConfigDescription("If the DungeonMinSize/DungeonMaxSize is above or below the next two settings, the dungeon size multiplier will aproximate to the value between the moon's specific dungeon size and this value.\nExample 1: If set to 0, the dungeon size will not be higher than DungeonMaxSize.\nExample 2: If set to 0.5, the dungeon size will be between the DungeonMaxSize and the moon's dungeon size multiplier.\nExample 3: If Set To 1, the dungeon size will be the moon's dungeon size multiplier with no restrictions.\nATTENTION: It is recommended to let it at default value or lower, the closer to 1 the bigger the dungeon.\nDefault: 0.8", (AcceptableValueBase)null, Array.Empty<object>()));
            configMinSize = Config.Bind("Size", "DungeonMinSize", 0.3f, new ConfigDescription("Input the minimum's dungeon size multiplier.\nDefault: 0.5", (AcceptableValueBase)null, Array.Empty<object>()));
            configMaxSize = Config.Bind("Size", "DungeonMaxSize", 0.45f, new ConfigDescription("Input the maximum's dungeon size multiplier.\nDefault: 0.65", (AcceptableValueBase)null, Array.Empty<object>()));

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
            BlackMesaExtendedDungeon.dungeonSizeMin = configMinSize.Value;
            BlackMesaExtendedDungeon.dungeonSizeMax = configMaxSize.Value;
            BlackMesaExtendedDungeon.dungeonSizeLerpPercentage = configDynamicValue.Value;
            BlackMesaExtendedDungeon.enableDynamicDungeonSizeRestriction = configDynamicToggle.Value;

            this.harmony.PatchAll(typeof(BlackMesaInterior));
            this.harmony.PatchAll(typeof(PatchStartOfRound));
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

        // new variables for size
        private ConfigEntry<float> configMinSize;
        private ConfigEntry<float> configMaxSize;
        private ConfigEntry<float> configDynamicValue;
        private ConfigEntry<bool> configDynamicToggle;

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

        // Patching Different item Group Mismatch
        [HarmonyPatch(typeof(UnityEngine.Object))]
        private class ItemGroupPatch
        {
            // Patch the Equals method
            [HarmonyPatch("Equals")]
            [HarmonyPrefix]
            public static bool FixItemGroupEquals(ref bool __result, object __instance, object other)
            {
                Debug.Log("IS THIS RUNNING ??!?!?!?!");
                // Cast the instance to ItemGroup if possible
                ItemGroup itemGroup = (ItemGroup)((__instance is ItemGroup) ? __instance : null);

                // If the cast was successful
                if (itemGroup != null)
                {
                    // Cast the other object to ItemGroup if possible
                    ItemGroup itemGroup2 = (ItemGroup)((other is ItemGroup) ? other : null);

                    // If the second cast was successful
                    if (itemGroup2 != null)
                    {
                        // Compare the itemSpawnTypeName properties of the two ItemGroups
                        __result = itemGroup.itemSpawnTypeName == itemGroup2.itemSpawnTypeName;

                        // Prevent the original Equals method from being executed
                        return false;
                    }
                }

                // Allow the original Equals method to be executed
                return true;
            }
        }
    }
}
