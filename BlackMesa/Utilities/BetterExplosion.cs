using BlackMesa.Components;
using GameNetcodeStuff;
using UnityEngine;

namespace BlackMesa.Utilities;

internal static class BetterExplosion
{
    private delegate void Hit(float distance);

    public static void SpawnExplosion(Vector3 explosionPosition, float killRange, float damageRange, int nonLethalDamage)
    {
        const int playersLayer = 3;
        const int roomLayer = 8;
        const int collidersLayer = 11;
        const int enemiesLayer = 19;
        const int mapHazardsLayer = 21;
        const int dealDamageToLayers = (1 << playersLayer) | (1 << enemiesLayer) | (1 << mapHazardsLayer);

        GameObject explosionPrefab = Object.Instantiate(StartOfRound.Instance.explosionPrefab, explosionPosition, Quaternion.Euler(-90f, 0f, 0f));
        explosionPrefab.AddComponent<DelayedDestruction>();
        explosionPrefab.SetActive(value: true);

        float cameraDistance = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, explosionPosition);
        HUDManager.Instance.ShakeCamera(cameraDistance < 14 ? ScreenShakeType.Big : ScreenShakeType.Small);

        Collider[] objectsToHit = Physics.OverlapSphere(explosionPosition, damageRange + 5, dealDamageToLayers, QueryTriggerInteraction.Collide);
        foreach (var objectToHit in objectsToHit)
        {
            Collider collider = null;
            Hit hit = null;

            if (objectToHit.gameObject.layer == playersLayer && objectToHit.GetComponent<PlayerControllerB>() is { } hitPlayer)
            {
                if (!hitPlayer.IsOwner)
                    continue;

                collider = hitPlayer.GetComponent<CharacterController>();
                hit = distance =>
                {
                    if (distance < killRange)
                    {
                        Vector3 bodyVelocity = Vector3.Normalize(hitPlayer.gameplayCamera.transform.position - explosionPosition) * 80f / Vector3.Distance(hitPlayer.gameplayCamera.transform.position, explosionPosition);
                        hitPlayer.KillPlayer(bodyVelocity, spawnBody: true, CauseOfDeath.Blast);
                    }
                    else if (distance < damageRange)
                    {
                        Vector3 bodyVelocity = Vector3.Normalize(hitPlayer.gameplayCamera.transform.position - explosionPosition) * 80f / Vector3.Distance(hitPlayer.gameplayCamera.transform.position, explosionPosition);
                        hitPlayer.DamagePlayer(nonLethalDamage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Blast, 0, fallDamage: false, bodyVelocity);
                    }
                };
            }
            else if (objectToHit.gameObject.layer == enemiesLayer && objectToHit.GetComponent<EnemyAICollisionDetect>() is { } hitEnemyCollider)
            {
                var hitEnemy = hitEnemyCollider.mainScript;
                if (!hitEnemy.IsOwner)
                    continue;

                collider = hitEnemyCollider.GetComponent<Collider>();
                hit = distance =>
                {
                    if (distance < damageRange * 0.75f)
                    {
                        hitEnemyCollider.mainScript.HitEnemyOnLocalClient(6);
                        hitEnemyCollider.mainScript.HitFromExplosion(distance);
                    }
                };
            }
            else if (objectToHit.gameObject.layer == mapHazardsLayer && objectToHit.GetComponent<Landmine>() is { IsOwner: true } landmine)
            {
                collider = landmine.GetComponent<Collider>();
                hit = distance =>
                {
                    if (distance < damageRange)
                    {
                        landmine.StartCoroutine(landmine.TriggerOtherMineDelayed(landmine));
                    }
                };
            }

            if (collider == null)
                continue;

            var closestPoint = collider.ClosestPoint(explosionPosition);
            if (Physics.Linecast(explosionPosition, closestPoint, out _, 1 << roomLayer, QueryTriggerInteraction.Ignore))
                continue;

            var distance = Vector3.Distance(explosionPosition, closestPoint);
            hit(distance);
        }

        objectsToHit = Physics.OverlapSphere(explosionPosition, 10, ~(1 << collidersLayer));
        foreach (var objectToHit in objectsToHit)
        {
            if (objectToHit.GetComponent<Rigidbody>() is Rigidbody rigidBody)
                rigidBody.AddExplosionForce(70, explosionPosition, 10);
        }
    }
}
