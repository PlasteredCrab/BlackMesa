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

            const string props = "Assets/LethalCompany/Mods/BlackMesaInterior/DunGen Stuff/Prefabs/Props";
            RegisterNetworkBehaviour(typeof(HandheldTVCamera), blackMesaAssets.LoadAsset<GameObject>($"{props}/HandheldTV.prefab"));
            RegisterNetworkBehaviour(typeof(Tripmine), blackMesaAssets.LoadAsset<GameObject>($"{props}/Tripmine.prefab"));
            RegisterNetworkBehaviour(typeof(HealingStation), blackMesaAssets.LoadAsset<GameObject>($"{props}/Healing Station.prefab"));
        }

        private static AudioMixerGroup[] mixerGroups;

        private static void RegisterNetworkBehaviour(Type type, GameObject prefab)
        {
            if (prefab == null)
            {
                Logger.LogError($"The prefab associated with NetworkBehaviour {type.Name} was not found");
                return;
            }
            if (!prefab.TryGetComponent<NetworkObject>(out _))
            {
                Logger.LogError($"The prefab {prefab} associated with NetworkBehaviour {type.Name} has no NetworkObject");
                return;
            }

            type.GetMethod("InitializeRPCS_" + type.Name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

            PatchNetworkManager.AddNetworkPrefab(prefab);

            if (mixerGroups == null)
                mixerGroups = Resources.FindObjectsOfTypeAll<AudioMixerGroup>();

            foreach (var audioSource in prefab.GetComponentsInChildren<AudioSource>())
            {
                if (audioSource.outputAudioMixerGroup == null)
                {
                    Logger.LogWarning($"{audioSource} on the prefab {prefab} containing NetworkBehaviour {type.Name} has a null output");
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
