using GameNetcodeStuff;
using UnityEngine;

namespace BlackMesa.Components;

public sealed class DummyEnemyAI : EnemyAI
{
    private IHittable hittable;

    private void Awake()
    {
        hittable = GetComponent<IHittable>();
        enabled = false;
    }

    public override void Start()
    {
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        hittable.Hit(force, Vector3.zero);
    }

    public override void OnDestroy()
    {
    }
}
