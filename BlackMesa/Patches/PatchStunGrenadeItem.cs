using BlackMesa.Components;
using HarmonyLib;
using System;
using UnityEngine;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(StunGrenadeItem))]
internal static class PatchStunGrenadeItem
{
    private static readonly Lazy<int> barnacleLayer = new(() => LayerMask.GetMask("MapHazards"));

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StunGrenadeItem.StunExplosion))]
    private static void StunExplosionPostfix(StunGrenadeItem __instance, Vector3 explosionPosition, float enemyStunTime)
    {
        var colliders = Physics.OverlapSphere(explosionPosition, 12, barnacleLayer.Value);

        foreach (var collider in colliders)
        {
            if (!collider.TryGetComponent<Barnacle>(out var barnacle))
            {
                if (!collider.TryGetComponent<BarnacleGrabTrigger>(out var trigger))
                    continue;
                barnacle = trigger.barnacle;
            }

            barnacle.Stun(enemyStunTime);
        }
    }
}
