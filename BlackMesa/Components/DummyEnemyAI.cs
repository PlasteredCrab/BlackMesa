using BlackMesa.Interfaces;
using GameNetcodeStuff;
using UnityEngine;

namespace BlackMesa.Components;

public sealed class DummyEnemyAI : EnemyAI
{
    private IDumbEnemy enemy;

    private void Awake()
    {
        enemy = GetComponent<IDumbEnemy>();
        enabled = false;
    }

    public override void Start()
    {
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        enemy.Hit(force, Vector3.zero);
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null)
    {
        if (!setToStunned)
            setToStunTime = 0;
        enemy.Stun(setToStunTime);
    }

    public override void OnCollideWithPlayer(Collider other)
    {
    }

    public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
    {
    }

    public override void Update()
    {
    }

    public override void KillEnemy(bool destroy = false)
    {
        enemy.Kill(destroy);
    }

    public override void EnableEnemyMesh(bool enable, bool overrideDoNotSet = false)
    {
    }

    public override void DoAIInterval()
    {
    }

    public override void OnDestroy()
    {
    }
}
