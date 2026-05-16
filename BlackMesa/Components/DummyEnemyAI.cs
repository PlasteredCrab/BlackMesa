using BlackMesa.Interfaces;
using GameNetcodeStuff;
using UnityEngine;

namespace BlackMesa.Components;

public sealed class DummyEnemyAI : EnemyAI
{
    public Object targetObject;

    private IDumbEnemy target;

    public override void Awake()
    {
        thisNetworkObject = NetworkObject;
        skinnedMeshRenderers = [];
        meshRenderers = [];
        overlapColliders = new Collider[1];
        allAINodes = [];
        path1 = new();
        enabled = false;

        target = (IDumbEnemy)targetObject;
    }

    public override void Start()
    {
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        target.Hit(force, Vector3.zero, playerWhoHit, playHitSFX, hitID);
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null)
    {
        if (!setToStunned)
            setToStunTime = 0;
        target.Stun(setToStunTime);
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
        target.Kill(destroy);
    }

    public override void EnableEnemyMesh(bool enable, bool overrideDoNotSet = false, bool tamperWithMeshes = false)
    {
    }

    public override void DoAIInterval()
    {
    }

    public override void OnDestroy()
    {
        RoundManager.Instance.SpawnedEnemies.Remove(this);
    }
}
