using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace BlackMesa.Utilities;

internal static class BetterExplosion
{
    private struct PlayerHit
    {
        int damage;

        internal void RegisterHit(int damage)
        {
            if (damage > this.damage || damage == -1)
            {
                BlackMesaInterior.Logger.LogInfo($"Register player damage {damage}");
                this.damage = damage;
            }
        }

        internal readonly void DoHit(Vector3 explosionPosition, PlayerControllerB player)
        {
            if (damage == 0)
                return;
            if (player == null)
                return;
            Vector3 bodyVelocity = Vector3.Normalize(player.gameplayCamera.transform.position - explosionPosition) * 80f / Vector3.Distance(player.gameplayCamera.transform.position, explosionPosition);
            BlackMesaInterior.Logger.LogInfo($"Hit {player} for {damage}");
            if (damage == -1)
                player.KillPlayer(bodyVelocity, spawnBody: true, CauseOfDeath.Blast);
            else
                player.DamagePlayer(damage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Blast, 0, fallDamage: false, bodyVelocity);
        }
    }

    private struct EnemyHit
    {
        int damage;
        float distance;

        internal void RegisterHit(int damage, float distance)
        {
            if (damage > this.damage)
                this.damage = damage;
            if (distance < this.distance)
                this.distance = distance;
        }

        internal readonly void DoHit(Vector3 explosionPosition, EnemyAI enemy)
        {
            if (damage == 0)
                return;
            if (enemy == null)
                return;
            BlackMesaInterior.Logger.LogInfo($"Hit {enemy} for {damage}");
            enemy.HitEnemyOnLocalClient(damage);
            enemy.HitFromExplosion(distance);
        }
    }

    private static readonly SequentialElementMap<PlayerControllerB, PlayerHit> playerHits = new(() => new PlayerHit(), player => (int)player.playerClientId, 4);
    private static readonly SequentialElementMap<EnemyAI, EnemyHit> enemyHits = new(() => new EnemyHit(), enemy => enemy.thisEnemyIndex, 4);

    public static int GetEnemyDamage(int nonLethalDamage)
    {
        return (nonLethalDamage + 10) / 20;
    }

    const int playersLayer = 3;
    const int roomLayer = 8;
    const int collidersLayer = 11;
    const int enemiesLayer = 19;
    const int mapHazardsLayer = 21;
    const int dealDamageToLayers = (1 << playersLayer) | (1 << enemiesLayer) | (1 << mapHazardsLayer);

    public static void SpawnExplosion(Vector3 explosionPosition, float killRange, float damageRange, int nonLethalDamage)
    {
        int enemyDamage = GetEnemyDamage(nonLethalDamage);

        GameObject explosionPrefab = Object.Instantiate(StartOfRound.Instance.explosionPrefab, explosionPosition, Quaternion.Euler(-90f, 0f, 0f));
        explosionPrefab.SetActive(value: true);

        var cameraDistance = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, explosionPosition);
        HUDManager.Instance.ShakeCamera(cameraDistance < 14 ? ScreenShakeType.Big : ScreenShakeType.Small);

        var objectsToHit = Physics.OverlapSphere(explosionPosition, damageRange + 5, dealDamageToLayers, QueryTriggerInteraction.Collide);

        playerHits.Clear();
        enemyHits.Clear();

        foreach (var objectToHit in objectsToHit)
        {
            var closestPoint = objectToHit.ClosestPoint(explosionPosition);
            var distance = Vector3.Distance(explosionPosition, closestPoint);

            if (Physics.Linecast(explosionPosition, closestPoint, out _, 1 << roomLayer, QueryTriggerInteraction.Ignore))
            {
                BlackMesaInterior.Logger.LogDebug($"Explosion target {objectToHit} blocked by collision");
                continue;
            }

            var layer = objectToHit.gameObject.layer;
            BlackMesaInterior.Logger.LogDebug($"Explosion trying to hit {objectToHit} on layer {layer} with distance {distance} (damage {damageRange}, kill {killRange})");

            if (layer == playersLayer && objectToHit.TryGetComponent(out PlayerControllerB hitPlayer))
            {
                if (!hitPlayer.IsOwner)
                    continue;

                if (distance <= killRange)
                    playerHits[hitPlayer].RegisterHit(-1);
                else if (distance <= damageRange)
                    playerHits[hitPlayer].RegisterHit(nonLethalDamage);
            }
            else if (layer == enemiesLayer && objectToHit.TryGetComponent(out EnemyAICollisionDetect hitEnemyCollider))
            {
                if (!hitEnemyCollider.mainScript.IsOwner)
                    continue;
                if (distance < damageRange * 0.75f)
                    enemyHits[hitEnemyCollider.mainScript].RegisterHit(enemyDamage, distance);
            }
            else if (layer == mapHazardsLayer && objectToHit.TryGetComponent(out Landmine hitLandmine) && hitLandmine.IsOwner)
            {
                if (distance < damageRange)
                    hitLandmine.StartCoroutine(hitLandmine.TriggerOtherMineDelayed(hitLandmine));
            }
            else if (objectToHit.TryGetComponent(out IHittable hittable))
            {
                hittable.Hit(enemyDamage, Vector3.Normalize(closestPoint - explosionPosition));
            }
        }

        foreach (var (player, hit) in playerHits)
            hit.DoHit(explosionPosition, player);
        foreach (var (enemy, hit) in enemyHits)
            hit.DoHit(explosionPosition, enemy);

        objectsToHit = Physics.OverlapSphere(explosionPosition, 10, ~(1 << collidersLayer));
        foreach (var objectToHit in objectsToHit)
        {
            if (objectToHit.TryGetComponent<Rigidbody>(out var rigidBody))
                rigidBody.AddExplosionForce(70, explosionPosition, 10);
        }
    }

    public static void DeadlySphereCastExplosion(Vector3 position, Vector3 direction, float radius, float range, int enemyDamage)
    {
        const int playersLayer = 3;
        const int roomLayer = 8;
        const int enemiesLayer = 19;
        const int dealDamageToLayers = (1 << playersLayer) | (1 << enemiesLayer);

        var ray = new Ray(position, direction);
        var hits = Physics.SphereCastAll(ray, radius, range, dealDamageToLayers, QueryTriggerInteraction.Ignore);

        var hitEnemies = new Dictionary<EnemyAI, float>();

        foreach (var hit in hits)
        {
            if (Physics.Linecast(position, hit.point, out _, 1 << roomLayer, QueryTriggerInteraction.Ignore))
                continue;

            if (hit.collider.TryGetComponent(out PlayerControllerB player))
            {
                if (!player.IsOwner)
                    continue;
                player.KillPlayer(direction, spawnBody: true, CauseOfDeath.Blast);
            }
            else if (hit.collider.TryGetComponent(out EnemyAICollisionDetect enemyCollider))
            {
                if (!enemyCollider.mainScript.IsOwner)
                    continue;
                if (hitEnemies.TryGetValue(enemyCollider.mainScript, out var hitDistance) && hit.distance > hitDistance)
                    continue;
                hitEnemies[enemyCollider.mainScript] = hit.distance;
            }
        }

        foreach (var (enemy, distance) in hitEnemies)
        {
            enemy.HitEnemy(enemyDamage);
            enemy.HitFromExplosion(distance);
        }
    }
}
