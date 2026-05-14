using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace BlackMesa.Components;

/// <summary>
/// Add this to a trigger collider (box/sphere/etc) to teleport the local player
/// and enemies to random interior positions using inverse-teleporter placement rules.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class InverseTeleporterTrigger : NetworkBehaviour
{
    private static readonly System.Random sfxRandomizer = new();

    [Header("Trigger")]
    [Min(0f)]
    public float retriggerCooldown = 1.5f;
    [Min(0f)]
    public float serverValidationDistance = 1.5f;

    [Header("Audio (Optional)")]
    public AudioSource teleportAudioSource;
    public AudioClip[] teleporterBeamUpSFX = [];

    [Header("Arrival FX (Player Optional)")]
    public ParticleSystem[] playerArrivalParticlePrefabs = [];
    public AudioClip[] playerArrivalSFX = [];
    [Range(0f, 1f)]
    public float playerArrivalSFXVolume = 1f;

    [Header("Cave Reverb (Optional)")]
    public ReverbPreset caveReverb;

    [Header("Randomness")]
    public int seedOffset = 17;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    [Min(0f)]
    public float debugLogCooldown = 1f;

    private Collider triggerCollider;
    private Rigidbody triggerRigidbody;
    private float nextUseTime;
    private readonly Dictionary<int, System.Random> serverSeeds = new Dictionary<int, System.Random>();
    private readonly Dictionary<ulong, System.Random> serverEnemySeeds = new Dictionary<ulong, System.Random>();
    private readonly Dictionary<int, float> serverNextAllowedUseTime = new Dictionary<int, float>();
    private readonly Dictionary<ulong, float> serverEnemyNextAllowedUseTime = new Dictionary<ulong, float>();
    private float nextAllowedDebugLogTime;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerRigidbody = GetComponent<Rigidbody>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: InverseTeleporterTrigger needs Is Trigger enabled.", this);
        }

        if (triggerRigidbody != null)
        {
            triggerRigidbody.isKinematic = true;
            triggerRigidbody.useGravity = false;
        }
    }

    private void OnEnable()
    {
        if (StartOfRound.Instance != null)
        {
            StartOfRound.Instance.StartNewRoundEvent.AddListener(SetRandomSeed);
        }

        SetRandomSeed();
    }

    private void OnDisable()
    {
        if (StartOfRound.Instance != null)
        {
            StartOfRound.Instance.StartNewRoundEvent.RemoveListener(SetRandomSeed);
        }
    }

    private void SetRandomSeed()
    {
        serverSeeds.Clear();
        serverEnemySeeds.Clear();
        serverNextAllowedUseTime.Clear();
        serverEnemyNextAllowedUseTime.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTriggerContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleTriggerContact(other);
    }

    private void HandleTriggerContact(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (!CanUseInverseTeleporter(out string rejectReason))
        {
            LogRateLimited($"Trigger touched by '{other.name}', but teleport blocked: {rejectReason}");
            return;
        }

        if (Time.time < nextUseTime)
        {
            return;
        }

        if (IsSpawned)
        {
            if (IsServer)
            {
                TryHandleEnemyTriggerOnServer(other);
            }

            if (IsClient)
            {
                TryHandlePlayerTriggerClient(other);
            }

            return;
        }

        // Fallback path for non-spawned objects so you can still test/work in-editor.
        LogRateLimited($"NetworkObject is not spawned yet. Using local fallback path. Collider: {other.name}");
        TryHandleEnemyTriggerFallback(other);
        TryHandlePlayerTriggerFallback(other);
    }

    private void TryHandlePlayerTriggerClient(Collider other)
    {
        PlayerControllerB localPlayer = GameNetworkManager.Instance?.localPlayerController;
        if (localPlayer == null || localPlayer.isPlayerDead)
        {
            return;
        }

        PlayerControllerB hitPlayer = other.GetComponentInParent<PlayerControllerB>();
        if (hitPlayer != localPlayer)
        {
            return;
        }

        nextUseTime = Time.time + retriggerCooldown;
        Log($"Player trigger hit ({other.name}) -> requesting server teleport.");
        RequestInverseTeleportServerRpc((int)localPlayer.playerClientId);
    }

    private void TryHandlePlayerTriggerFallback(Collider other)
    {
        PlayerControllerB localPlayer = GameNetworkManager.Instance?.localPlayerController;
        if (localPlayer == null || localPlayer.isPlayerDead)
        {
            return;
        }

        PlayerControllerB hitPlayer = other.GetComponentInParent<PlayerControllerB>();
        if (hitPlayer != localPlayer)
        {
            if (hitPlayer == null)
            {
                LogRateLimited($"Collider '{other.name}' did not resolve to PlayerControllerB (client path).");
            }
            return;
        }

        nextUseTime = Time.time + retriggerCooldown;
        Vector3 teleportPos = GetInverseTelePosition(GetOrCreateServerSeed((int)localPlayer.playerClientId));
        Log($"Fallback player teleport at {teleportPos} (NetworkObject not spawned).");
        TeleportPlayerWithInverseRules(localPlayer, teleportPos);
    }

    private void TryHandleEnemyTriggerOnServer(Collider other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy == null || enemy.isEnemyDead || enemy.NetworkObject == null)
        {
            if (enemy == null)
            {
                LogRateLimited($"Collider '{other.name}' did not resolve to EnemyAI (server path).");
            }
            return;
        }

        ulong enemyNetworkObjectId = enemy.NetworkObjectId;
        if (serverEnemyNextAllowedUseTime.TryGetValue(enemyNetworkObjectId, out float nextAllowed) && Time.time < nextAllowed)
        {
            return;
        }
        serverEnemyNextAllowedUseTime[enemyNetworkObjectId] = Time.time + retriggerCooldown;

        if (triggerCollider != null)
        {
            Vector3 closestPoint = triggerCollider.ClosestPoint(enemy.transform.position);
            float triggerDistance = Vector3.Distance(closestPoint, enemy.transform.position);
            if (triggerDistance > serverValidationDistance)
            {
                return;
            }
        }

        if (RoundManager.Instance == null || RoundManager.Instance.insideAINodes == null || RoundManager.Instance.insideAINodes.Length == 0)
        {
            return;
        }

        Vector3 teleportPos = GetInverseTelePosition(GetOrCreateEnemySeed(enemyNetworkObjectId));
        Log($"Enemy trigger hit ({enemy.name}) -> RPC teleport to {teleportPos}.");
        ApplyInverseEnemyTeleportClientRpc(enemyNetworkObjectId, teleportPos);
    }

    private void TryHandleEnemyTriggerFallback(Collider other)
    {
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy == null || enemy.isEnemyDead)
        {
            return;
        }

        ulong seedId = enemy.NetworkObject != null ? enemy.NetworkObjectId : (ulong)Mathf.Abs(enemy.GetInstanceID());
        if (serverEnemyNextAllowedUseTime.TryGetValue(seedId, out float nextAllowed) && Time.time < nextAllowed)
        {
            return;
        }
        serverEnemyNextAllowedUseTime[seedId] = Time.time + retriggerCooldown;

        Vector3 teleportPos = GetInverseTelePosition(GetOrCreateEnemySeed(seedId));
        Log($"Fallback enemy teleport ({enemy.name}) to {teleportPos} (NetworkObject not spawned).");
        TeleportEnemyWithInverseRules(enemy, teleportPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInverseTeleportServerRpc(int playerId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || StartOfRound.Instance == null || RoundManager.Instance == null)
        {
            return;
        }

        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        if (players == null || playerId < 0 || playerId >= players.Length)
        {
            return;
        }

        if ((int)rpcParams.Receive.SenderClientId != playerId)
        {
            Log($"Rejected teleport request: sender {rpcParams.Receive.SenderClientId} != playerId {playerId}.");
            return;
        }

        if (!CanUseInverseTeleporter(out string rejectReason))
        {
            Log($"Rejected player teleport request due to rules: {rejectReason}");
            return;
        }

        if (serverNextAllowedUseTime.TryGetValue(playerId, out float nextAllowed) && Time.time < nextAllowed)
        {
            return;
        }
        serverNextAllowedUseTime[playerId] = Time.time + retriggerCooldown;

        PlayerControllerB player = players[playerId];
        if (player == null || player.isPlayerDead)
        {
            return;
        }

        if (triggerCollider != null)
        {
            Vector3 closestPoint = triggerCollider.ClosestPoint(player.transform.position);
            float triggerDistance = Vector3.Distance(closestPoint, player.transform.position);
            if (triggerDistance > serverValidationDistance)
            {
                return;
            }
        }

        if (RoundManager.Instance.insideAINodes == null || RoundManager.Instance.insideAINodes.Length == 0)
        {
            return;
        }

        Vector3 teleportPos = GetInverseTelePositionForPlayer(playerId);
        Log($"Server accepted player teleport request for player {playerId}. Position: {teleportPos}");
        ApplyInverseTeleportClientRpc(playerId, teleportPos);
    }

    [ClientRpc]
    private void ApplyInverseTeleportClientRpc(int playerId, Vector3 teleportPos)
    {
        if (StartOfRound.Instance == null)
        {
            return;
        }

        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        if (players == null || playerId < 0 || playerId >= players.Length)
        {
            return;
        }

        PlayerControllerB player = players[playerId];
        Log($"Applying player teleport on client for player {playerId} to {teleportPos}");
        TeleportPlayerWithInverseRules(player, teleportPos);
    }

    [ClientRpc]
    private void ApplyInverseEnemyTeleportClientRpc(ulong enemyNetworkObjectId, Vector3 teleportPos)
    {
        if (!TryResolveEnemy(enemyNetworkObjectId, out EnemyAI enemy))
        {
            Log($"Could not resolve enemy by NetworkObjectId {enemyNetworkObjectId} on client.");
            return;
        }

        Log($"Applying enemy teleport on client for {enemy.name} ({enemyNetworkObjectId}) to {teleportPos}");
        TeleportEnemyWithInverseRules(enemy, teleportPos);
    }

    private bool CanUseInverseTeleporter(out string reason)
    {
        if (RoundManager.Instance == null)
        {
            reason = "RoundManager.Instance is null";
            return false;
        }

        if (RoundManager.Instance.insideAINodes == null || RoundManager.Instance.insideAINodes.Length == 0)
        {
            reason = "insideAINodes are not available yet";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private System.Random GetOrCreateServerSeed(int playerId)
    {
        if (serverSeeds.TryGetValue(playerId, out System.Random random))
        {
            return random;
        }

        int mapSeed = StartOfRound.Instance != null ? StartOfRound.Instance.randomMapSeed : 0;
        random = new System.Random(mapSeed + seedOffset + playerId);
        serverSeeds[playerId] = random;
        return random;
    }

    private System.Random GetOrCreateEnemySeed(ulong enemyNetworkObjectId)
    {
        if (serverEnemySeeds.TryGetValue(enemyNetworkObjectId, out System.Random random))
        {
            return random;
        }

        int mapSeed = StartOfRound.Instance != null ? StartOfRound.Instance.randomMapSeed : 0;
        int enemySeedOffset = 100000 + (int)(enemyNetworkObjectId % int.MaxValue);
        random = new System.Random(mapSeed + seedOffset + enemySeedOffset);
        serverEnemySeeds[enemyNetworkObjectId] = random;
        return random;
    }

    private Vector3 GetInverseTelePositionForPlayer(int playerId)
    {
        return GetInverseTelePosition(GetOrCreateServerSeed(playerId));
    }

    private Vector3 GetInverseTelePosition(System.Random random)
    {
        if (RoundManager.Instance == null || RoundManager.Instance.insideAINodes == null || RoundManager.Instance.insideAINodes.Length == 0)
        {
            return transform.position;
        }

        int navMask = 1537;
        int wallMask = 1375734017;
        int maxAttempts = Mathf.Min(12, RoundManager.Instance.insideAINodes.Length);

        Vector3 position = RoundManager.Instance.insideAINodes[random.Next(0, RoundManager.Instance.insideAINodes.Length)].transform.position;

        for (int i = 0; i < maxAttempts; i++)
        {
            if (i != 0)
            {
                position = RoundManager.Instance.insideAINodes[random.Next(0, RoundManager.Instance.insideAINodes.Length)].transform.position;
            }

            position = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(position, 10f, default, random, navMask);

            if (!RoundManager.Instance.GotNavMeshPositionResult || !NavMesh.FindClosestEdge(position, out NavMeshHit edgeHit, navMask))
            {
                continue;
            }

            Ray ray;
            if (edgeHit.position == position)
            {
                Vector3 offset = new Vector3(RoundManager.Instance.randomPositionInBoxRadius.x, edgeHit.position.y, RoundManager.Instance.randomPositionInBoxRadius.z);
                ray = new Ray(edgeHit.position + Vector3.up * 0.5f, edgeHit.position - offset);
            }
            else
            {
                ray = new Ray(edgeHit.position + Vector3.up * 0.5f, position - edgeHit.position);
            }

            if (Physics.Raycast(ray, out RaycastHit wallHit, 5f, wallMask, QueryTriggerInteraction.Ignore))
            {
                if (wallHit.distance >= 0.35f)
                {
                    position = RoundManager.Instance.GetNavMeshPosition(ray.GetPoint(wallHit.distance / 2f), default, 2f, navMask);
                    break;
                }

                ray.origin += Vector3.Normalize(ray.direction * 1000f) * 0.4f;
                if (!Physics.Raycast(ray, out wallHit, 5f, wallMask, QueryTriggerInteraction.Ignore))
                {
                    position = RoundManager.Instance.GetNavMeshPosition(ray.GetPoint(2.5f), default, 2f, navMask);
                    break;
                }

                if (wallHit.distance > 0.35f)
                {
                    position = RoundManager.Instance.GetNavMeshPosition(ray.GetPoint(wallHit.distance / 2f), default, 2f, navMask);
                    break;
                }
            }

            position = RoundManager.Instance.GetNavMeshPosition(ray.GetPoint(2.5f), default, 2f, navMask);
        }

        return position;
    }

    private bool TryResolveEnemy(ulong enemyNetworkObjectId, out EnemyAI enemy)
    {
        enemy = null;
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.SpawnManager == null)
        {
            return false;
        }

        if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject enemyObject) || enemyObject == null)
        {
            return false;
        }

        enemy = enemyObject.GetComponent<EnemyAI>();
        return enemy != null;
    }

    private void TeleportPlayerWithInverseRules(PlayerControllerB player, Vector3 teleportPos)
    {
        if (player == null || player.isPlayerDead)
        {
            return;
        }

        SpikeTrapsReactToInverseTeleport();
        SetCaveReverb(player);

        player.shipTeleporterId = -1;
        player.DropAllHeldItems();

        AudioReverbPresets reverbPresets = FindObjectOfType<AudioReverbPresets>();
        if (reverbPresets != null && reverbPresets.audioPresets != null && reverbPresets.audioPresets.Length > 2)
        {
            reverbPresets.audioPresets[2].ChangeAudioReverbForPlayer(player);
        }

        player.isInElevator = false;
        player.isInHangarShipRoom = false;
        player.isInsideFactory = true;
        player.averageVelocity = 0f;
        player.velocityLastFrame = Vector3.zero;

        player.TeleportPlayer(teleportPos);
        PlayPlayerArrivalFx(teleportPos);

        if (player.beamOutParticle != null)
        {
            player.beamOutParticle.Play();
        }

        AudioClip sfx = ResolveBeamSfx();
        if (teleportAudioSource != null && sfx != null)
        {
            teleportAudioSource.PlayOneShot(sfx);
        }

        if (player.movementAudio != null && sfx != null)
        {
            player.movementAudio.PlayOneShot(sfx);
        }

        if (player == GameNetworkManager.Instance?.localPlayerController)
        {
            HUDManager.Instance?.ShakeCamera(ScreenShakeType.Big);
        }
    }

    private void PlayPlayerArrivalFx(Vector3 position)
    {
        if (playerArrivalParticlePrefabs != null)
        {
            for (int i = 0; i < playerArrivalParticlePrefabs.Length; i++)
            {
                SpawnAndPlayArrivalParticle(playerArrivalParticlePrefabs[i], position);
            }
        }

        AudioClip sfx = GetRandomClip(playerArrivalSFX);
        if (sfx != null)
        {
            AudioSource.PlayClipAtPoint(sfx, position, playerArrivalSFXVolume);
        }
    }

    private void TeleportEnemyWithInverseRules(EnemyAI enemy, Vector3 teleportPos)
    {
        if (enemy == null || enemy.isEnemyDead)
        {
            return;
        }

        enemy.serverPosition = teleportPos;
        enemy.destination = teleportPos;

        if (enemy.isOutside)
        {
            enemy.SetEnemyOutside(false);
        }

        if (enemy.agent != null && enemy.agent.enabled && enemy.agent.isOnNavMesh)
        {
            enemy.agent.Warp(teleportPos);
        }
        else
        {
            bool wasEnabled = enemy.agent != null && enemy.agent.enabled;
            if (enemy.agent != null && wasEnabled)
            {
                enemy.agent.enabled = false;
            }

            enemy.transform.position = teleportPos;

            if (enemy.agent != null && wasEnabled)
            {
                enemy.agent.enabled = true;
            }
        }
    }

    private void SpikeTrapsReactToInverseTeleport()
    {
        SpikeRoofTrap[] traps = FindObjectsOfType<SpikeRoofTrap>(includeInactive: false);
        if (traps == null)
        {
            return;
        }

        for (int i = 0; i < traps.Length; i++)
        {
            if (traps[i] == null)
            {
                continue;
            }

            traps[i].timeSinceMovingUp = Time.realtimeSinceStartup - 1f;
        }
    }

    private void SetCaveReverb(PlayerControllerB player)
    {
        if (player == null || caveReverb == null || RoundManager.Instance == null || RoundManager.Instance.currentDungeonType != 4)
        {
            return;
        }

        GameObject[] caveNodes = RoundManager.Instance.allCaveNodes;
        if (caveNodes == null)
        {
            return;
        }

        for (int i = 0; i < caveNodes.Length; i++)
        {
            if (caveNodes[i] == null)
            {
                continue;
            }

            if (Vector3.Distance(caveNodes[i].transform.position, player.transform.position) < 12f)
            {
                player.reverbPreset = caveReverb;
                break;
            }
        }
    }

    private AudioClip ResolveBeamSfx()
    {
        AudioClip sfx = GetRandomClip(teleporterBeamUpSFX);
        if (sfx != null)
        {
            return sfx;
        }

        ShipTeleporter teleporter = FindObjectOfType<ShipTeleporter>();
        if (teleporter != null)
        {
            return teleporter.teleporterBeamUpSFX;
        }

        return null;
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int nonNullCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                nonNullCount++;
            }
        }

        if (nonNullCount == 0)
        {
            return null;
        }

        int selectedNonNullIndex = sfxRandomizer.Next(nonNullCount);
        int currentNonNullIndex = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            if (currentNonNullIndex == selectedNonNullIndex)
            {
                return clip;
            }

            currentNonNullIndex++;
        }

        return null;
    }

    private static void SpawnAndPlayArrivalParticle(ParticleSystem prefab, Vector3 position)
    {
        if (prefab == null)
        {
            return;
        }

        ParticleSystem particles = Instantiate(prefab, position, Quaternion.identity);
        particles.Play();

        ParticleSystem.MainModule main = particles.main;
        float lifetime = 3f;
        if (!main.loop)
        {
            lifetime = main.duration + main.startLifetime.constantMax + 0.25f;
        }

        Destroy(particles.gameObject, lifetime);
    }

    private void Log(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log($"[InverseTeleporterTrigger:{name}] {message}", this);
    }

    private void LogRateLimited(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        if (Time.time < nextAllowedDebugLogTime)
        {
            return;
        }

        nextAllowedDebugLogTime = Time.time + debugLogCooldown;
        Debug.Log($"[InverseTeleporterTrigger:{name}] {message}", this);
    }
}
