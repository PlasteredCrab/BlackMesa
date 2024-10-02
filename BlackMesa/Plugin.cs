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
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

namespace BlackMesa
{

    [BepInPlugin(GUID, NAME, VERSION)]
    public class BlackMesaInterior : BaseUnityPlugin
    {
        public const string GUID = "Plastered_Crab.BlackMesaInterior";
        public const string NAME = "Black Mesa Interior";
        public const string VERSION = "1.1.0";

        public static BlackMesaInterior Instance;

        private readonly Harmony harmony = new(GUID);

        new internal static ManualLogSource Logger;

        internal static DungeonFlow BlackMesaFlow;

        internal static AssetBundle Assets;

        // Awake method is called before the Menu Screen initialization
        private void Awake()
        {
            // Store the plugin as a singleton instance.
            Instance = this;

            // Store a logger in a static field for use throughout the mod.
            Logger = base.Logger;

            // Load Interior Dungeon assets from the AssetBundle.
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assets = AssetBundle.LoadFromFile(Path.Combine(directoryName, "blackmesainterior"));
            if (Assets == null)
            {
                Logger.LogError("Failed to load Interior Dungeon assets.");
                return;
            }
            Logger.LogInfo("Interior Assets loaded successfully.");

            // Retrieve the Extended Dungeon Flow from the AssetBundle.
            ExtendedDungeonFlow blackMesaExtendedDungeon = Assets.LoadAsset<ExtendedDungeonFlow>("Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Black Mesa Extended Flow.asset");
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
            harmony.PatchAll(typeof(PatchDungeonGenerator));
            harmony.PatchAll(typeof(PatchLungProp));

            const string props = "Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Prefabs/Props";
            InitializeNetworkBehaviour(typeof(HandheldTVCamera));
            RegisterNetworkPrefab($"{props}/HandheldTV.prefab");

            InitializeNetworkBehaviour(typeof(Tripmine));
            RegisterNetworkPrefab($"{props}/Tripmine.prefab");

            InitializeNetworkBehaviour(typeof(StationBase));
            InitializeNetworkBehaviour(typeof(HealingStation));
            RegisterNetworkPrefab($"{props}/Healing Station.prefab");
            InitializeNetworkBehaviour(typeof(ChargingStation));
            RegisterNetworkPrefab($"{props}/HEV Station 1.prefab");
        }

        private static AudioMixerGroup[] mixerGroups;

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
            var prefab = Assets.LoadAsset<GameObject>(path);

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

            // Fix up the mixer groups for all audio sources.
            mixerGroups ??= Resources.FindObjectsOfTypeAll<AudioMixerGroup>();

            foreach (var audioSource in prefab.GetComponentsInChildren<AudioSource>())
            {
                if (audioSource.outputAudioMixerGroup == null)
                {
                    Logger.LogWarning($"{audioSource} on the prefab {prefab} from path \"{path}\" has a null output.");
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

}
