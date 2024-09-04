using GameNetcodeStuff;
using System;
using UnityEngine;

namespace BlackMesa;

internal static class BetterExplosion
{
    public static void SpawnExplosion(Vector3 explosionPosition, float killRange, float damageRange, int nonLethalDamage)
    {
        killRange = Math.Min(killRange, damageRange);

        const int playersLayer = 3;
        const int roomLayer = 8;
        const int collidersLayer = 11;

        GameObject explosionPrefab = UnityEngine.Object.Instantiate(StartOfRound.Instance.explosionPrefab, explosionPosition, Quaternion.Euler(-90f, 0f, 0f), RoundManager.Instance.mapPropsContainer.transform);
        explosionPrefab.SetActive(value: true);

        float cameraDistance = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, explosionPosition);
        HUDManager.Instance.ShakeCamera(cameraDistance < 14 ? ScreenShakeType.Big : ScreenShakeType.Small);

        Collider[] objectsToHit = Physics.OverlapSphere(explosionPosition, damageRange + 5, 1 << playersLayer, QueryTriggerInteraction.Collide);
        foreach (var objectToHit in objectsToHit)
        {
            var hitPlayer = objectToHit.gameObject.GetComponent<PlayerControllerB>();
            if (hitPlayer == null)
                continue;
            var hitCollider = hitPlayer.GetComponent<CharacterController>();
            if (hitCollider == null)
                continue;
            var closestPoint = hitCollider.ClosestPoint(explosionPosition);

            if (Physics.Linecast(explosionPosition, closestPoint, out _, 1 << roomLayer, QueryTriggerInteraction.Ignore))
                continue;

            if (hitPlayer != null && hitPlayer.IsOwner && hitCollider != null)
            {
                float actualDistance = Vector3.Distance(explosionPosition, closestPoint);
                if (actualDistance < killRange)
                {
                    Vector3 bodyVelocity = Vector3.Normalize(hitPlayer.gameplayCamera.transform.position - explosionPosition) * 80f / Vector3.Distance(hitPlayer.gameplayCamera.transform.position, explosionPosition);
                    hitPlayer.KillPlayer(bodyVelocity, spawnBody: true, CauseOfDeath.Blast);
                }
                else if (actualDistance < damageRange)
                {
                    Vector3 bodyVelocity = Vector3.Normalize(hitPlayer.gameplayCamera.transform.position - explosionPosition) * 80f / Vector3.Distance(hitPlayer.gameplayCamera.transform.position, explosionPosition);
                    hitPlayer.DamagePlayer(nonLethalDamage, hasDamageSFX: true, callRPC: true, CauseOfDeath.Blast, 0, fallDamage: false, bodyVelocity);
                }
            }
        }

        objectsToHit = Physics.OverlapSphere(explosionPosition, 10, ~(1 << collidersLayer));
        foreach (var objectToHit in objectsToHit)
        {
            if (objectToHit.GetComponent<Rigidbody>() is Rigidbody rigidBody)
                rigidBody.AddExplosionForce(70, explosionPosition, 10);
        }
    }
}
