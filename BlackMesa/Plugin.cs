using BepInEx;
using BepInEx.Logging;
using BlackMesa.Components;
using BlackMesa.Patches;
using BlackMesa.Scriptables;
using DunGen;
using DunGen.Graph;
using HarmonyLib;
using LethalLevelLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace BlackMesa
{

    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("imabatby.lethallevelloader")]
    public class BlackMesaInterior : BaseUnityPlugin
    {
        public const string GUID = "Plastered_Crab.BlackMesaInterior";
        public const string NAME = "Black Mesa Interior";
        public const string VERSION = "3.1.3";

        public static BlackMesaInterior Instance;

        private readonly Harmony harmony = new(GUID);

        new internal static ManualLogSource Logger;

        internal static DungeonFlow BlackMesaFlow;

        internal static AssetBundle Assets;

        internal static GameObject GenerationRulesPrefab;

        internal static List<GameObject> PrefabsWithAudioSources = [];

        // Awake method is called before the Menu Screen initialization
        private void Awake()
        {
            // Store the plugin as a singleton instance.
            Instance = this;

            // Store a logger in a static field for use throughout the mod.
            Logger = base.Logger;

            // Load Interior Dungeon assets from the AssetBundle.
            string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assets = AssetBundle.LoadFromFile(Path.Combine(assemblyPath, "blackmesainterior"));
            if (Assets == null)
            {
                if (Application.isEditor)
                {
                    Logger.LogWarning("Failed to load interior dungeon assets, attempting to continue by loading resources by path.");
                }
                else
                {
                    Logger.LogError("Failed to load interior dungeon assets.");
                    return;
                }
            }
            else
            {
                Logger.LogInfo("Interior assets loaded successfully.");
            }

            var serializationCheck = LoadAsset<FixPluginTypesSerializationCheck>("Assets/LethalCompany/Mods/BlackMesaInterior/FixPluginTypesSerializationCheck.asset");
            if (serializationCheck == null)
            {
                Logger.LogError($"FixPluginTypesSerialization checker was not loaded.");
                return;
            }
            if (!serializationCheck.IsWorking)
            {
                Logger.LogError($"FixPluginTypesSerialization is not working, please copy its config from someone that has no errors during FixPluginTypesSerialization startup.");
                return;
            }

            // Load and register the mod to LethalLevelLoader.
            ExtendedMod blackMesaExtendedMod = LoadAsset<ExtendedMod>("Assets/LethalCompany/Mods/BlackMesaInterior/BlackMesaInteriorMod.asset");
            if (blackMesaExtendedMod == null)
            {
                Logger.LogError("Failed to load the interior assets. Stopping.");
                return;
            }
            if (blackMesaExtendedMod.ExtendedDungeonFlows.Count < 1)
            {
                Logger.LogError("The extended mod has no dungeon flow.");
                return;
            }

            var dunGenExtender = LoadAsset<DunGenPlus.DunGenExtender>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/DunGenPlus/DunGenExtender.asset");
            if (dunGenExtender == null)
            {
                Logger.LogError("Failed to find DunGenPlus configuration. Stopping.");
                return;
            }
            DunGenPlus.API.AddDunGenExtender(dunGenExtender);

            GenerationRulesPrefab = LoadAsset<GameObject>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/GenerationRules.prefab");
            if (GenerationRulesPrefab == null)
            {
                Logger.LogError("Failed to find generation rules prefab. Stopping.");
                return;
            }

            BlackMesaFlow = blackMesaExtendedMod.ExtendedDungeonFlows[0].DungeonFlow;

            // Register the Extended Dungeon Flow with LLL.
            PatchedContent.RegisterExtendedMod(blackMesaExtendedMod);
            Logger.LogInfo("Loaded and registered interior assets.");

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

            const string prefabs = "Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Prefabs";

            LoadAsset<DiffusionProfileMappings>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Diffusion Profile Mappings.asset").Apply();

            #region Register hazards

            const string hazards = $"{prefabs}/Hazards";

            InitializeNetworkBehaviour(typeof(Barnacle));
            InitializeNetworkBehaviour(typeof(BarnacleSounds));
            RegisterNetworkPrefab($"{hazards}/Barnacle/Barnacle.prefab");

            InitializeNetworkBehaviour(typeof(Tripmine));
            RegisterNetworkPrefab($"{hazards}/Laser Tripmine/Tripmine.prefab");

            #endregion

            #region Register props

            const string props = $"{prefabs}/Props";

            InitializeNetworkBehaviour(typeof(HandheldTVCamera));
            RegisterNetworkPrefab($"{props}/HandheldTV.prefab");

            InitializeNetworkBehaviour(typeof(StationBase));
            InitializeNetworkBehaviour(typeof(HealingStation));
            RegisterNetworkPrefab($"{props}/Healing Station.prefab");
            InitializeNetworkBehaviour(typeof(ChargingStation));
            RegisterNetworkPrefab($"{props}/HEV Station.prefab");

            #endregion
        }

        internal static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            if (Assets != null)
                return Assets.LoadAsset<T>(path);

            if (!Application.isEditor)
                return null;

            try
            {
                return TryToLoadAssetDirectlyInEditor<T>(path);
            }
            catch (FileNotFoundException)
            {
                Logger.LogError($"Editor assembly is not present.");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static T TryToLoadAssetDirectlyInEditor<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void InitializeNetworkBehaviour(Type type)
        {
            // Call the RPC initializer methods to register the RPC handlers
            var initializer = type.GetMethod("InitializeRPCS_" + type.Name, BindingFlags.Static | BindingFlags.NonPublic);
            if (initializer == null)
            {
                Logger.LogError($"{type} does not have a static RPC initializer method.");
                return;
            }

            initializer.Invoke(null, null);
        }

        private static void RegisterNetworkPrefab(string path)
        {
            var prefab = LoadAsset<GameObject>(path);

            if (prefab == null)
            {
                Logger.LogError($"The prefab \"{path}\" was not found.");
                return;
            }
            if (!prefab.TryGetComponent<NetworkObject>(out _))
            {
                Logger.LogError($"The prefab {prefab} from path \"{path}\" has no NetworkObject.");
                return;
            }

            // Register the prefab to Unity netcode.
            PatchNetworkManager.AddNetworkPrefab(prefab);

            PrefabsWithAudioSources.Add(prefab);
        }

        private static List<AudioMixerGroup> GetVanillaAudioMixerGroups()
        {
            var seenMixerNames = new HashSet<string>();
            var mixerGroups = new List<AudioMixerGroup>();

            foreach (var mixer in Resources.FindObjectsOfTypeAll<AudioMixer>())
            {
                if (seenMixerNames.Contains(mixer.name))
                    continue;
                seenMixerNames.Add(mixer.name);
                foreach (var mixerGroup in mixer.FindMatchingGroups(""))
                    mixerGroups.Add(mixerGroup);
            }

            return mixerGroups;
        }

        internal static void FixAudioSources()
        {
            var mixerGroups = GetVanillaAudioMixerGroups();

            foreach (var prefab in PrefabsWithAudioSources)
            {
                foreach (var audioSource in prefab.GetComponentsInChildren<AudioSource>())
                {
                    if (audioSource.outputAudioMixerGroup == null)
                    {
                        Logger.LogWarning($"{audioSource} on the prefab {prefab} has a null output.");
                        continue;
                    }
                    AudioMixerGroup mixerGroup = null;
                    foreach (var candidateMixerGroup in mixerGroups)
                    {
                        if (candidateMixerGroup.name == audioSource.outputAudioMixerGroup.name)
                        {
                            mixerGroup = candidateMixerGroup;
                            break;
                        }
                    }
                    if (mixerGroup == null)
                        continue;
                    audioSource.outputAudioMixerGroup = mixerGroup;
                }
            }
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
