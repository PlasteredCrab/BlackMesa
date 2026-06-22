using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

public class FlashbangGrenadeItem : GrabbableObject
{
    [Header("Behavior")]
    public float fuseDuration = 2.25f;
    public GameObject explosionEffect;
    [Space(3f)]
    public float explodedValueMultiplier = 1f;

    [Header("Visuals")]
    public Animator itemAnimator;
    public AudioSource itemAudio;
    public AudioClip pullPinSFX;
    public AudioClip explodeSFX;

    [Header("Physics")]
    public AnimationCurve grenadeFallCurve;
    public AnimationCurve grenadeVerticalFallCurve;
    public AnimationCurve grenadeVerticalFallCurveNoBounce;
    public LayerMask throwRaycastMask = 0x10000901;

    private Coroutine pullPinCoroutine;
    private bool inPullingPinAnimation;
    private bool pinPulled;
    private float explosionTime;
    private PlayerControllerB playerThrownBy;
    private bool hasExploded;

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);

        if (inPullingPinAnimation)
            return;
        if (!pinPulled)
        {
            if (pullPinCoroutine == null)
            {
                playerHeldBy.activatingItem = true;
                pullPinCoroutine = StartCoroutine(PullPinAnimation());
            }
            return;
        }

        if (IsOwner)
            playerHeldBy.DiscardHeldObject(placeObject: true, null, GetGrenadeThrowDestination());
    }

    public override void DiscardItem()
    {
        if (playerHeldBy != null)
            playerHeldBy.activatingItem = false;
        base.DiscardItem();
    }

    public override void SetControlTipsForItem()
    {
        if (!pinPulled)
        {
            HUDManager.Instance.ChangeControlTipMultiple(["Pull pin: [RMB]"], holdingItem: true, itemProperties);
            return;
        }
        HUDManager.Instance.ChangeControlTipMultiple(["Throw grenade: [RMB]"], holdingItem: true, itemProperties);
    }

    public override void FallWithCurve()
    {
        float magnitude = Vector3.Distance(startFallingPosition, targetFloorPosition);
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(itemProperties.restingRotation.x, transform.eulerAngles.y, itemProperties.restingRotation.z), 14f * Time.deltaTime / magnitude);
        transform.localPosition = Vector3.Lerp(startFallingPosition, targetFloorPosition, grenadeFallCurve.Evaluate(fallTime));
        if (magnitude > 5f)
            transform.localPosition = Vector3.Lerp(new Vector3(transform.localPosition.x, startFallingPosition.y, transform.localPosition.z), new Vector3(transform.localPosition.x, targetFloorPosition.y, transform.localPosition.z), grenadeVerticalFallCurveNoBounce.Evaluate(fallTime));
        else
            transform.localPosition = Vector3.Lerp(new Vector3(transform.localPosition.x, startFallingPosition.y, transform.localPosition.z), new Vector3(transform.localPosition.x, targetFloorPosition.y, transform.localPosition.z), grenadeVerticalFallCurve.Evaluate(fallTime));
        fallTime += Mathf.Abs(Time.deltaTime * 12f / magnitude);
    }

    private IEnumerator PullPinAnimation()
    {
        inPullingPinAnimation = true;
        playerHeldBy.activatingItem = true;
        playerHeldBy.doingUpperBodyEmote = 1.16f;
        playerHeldBy.playerBodyAnimator.SetTrigger("PullGrenadePin");
        itemAnimator.SetTrigger("pullPin");
        itemAudio.PlayOneShot(pullPinSFX);
        WalkieTalkie.TransmitOneShotAudio(itemAudio, pullPinSFX, vol: 0.8f);
        yield return new WaitForSeconds(1f);
        if (playerHeldBy != null)
        {
            playerHeldBy.activatingItem = false;
            playerThrownBy = playerHeldBy;
        }
        inPullingPinAnimation = false;
        pinPulled = true;
        itemUsedUp = true;
        if (playerHeldBy != null)
            SetControlTipsForItem();
        explosionTime = Time.time + fuseDuration;
    }

    public override void Update()
    {
        base.Update();

        if (pinPulled && !hasExploded && Time.time >= explosionTime)
            ExplodeStunGrenade();
    }

    private void ExplodeStunGrenade()
    {
        if (hasExploded)
            return;
        hasExploded = true;
        Transform parent = isInElevator ? StartOfRound.Instance.elevatorTransform : RoundManager.Instance.mapPropsContainer.transform;
        StunGrenadeItem.StunExplosion(transform.position, affectAudio: true, flashSeverityMultiplier: 1f, enemyStunTime: 7.5f, flashSeverityDistanceRolloff: 1f, isHeld, playerHeldBy, playerThrownBy);
        Instantiate(explosionEffect, transform.position, Quaternion.identity, parent);
        itemAudio.PlayOneShot(explodeSFX);
        WalkieTalkie.TransmitOneShotAudio(itemAudio, explodeSFX);

        SetScrapValue((int)(scrapValue * explodedValueMultiplier));
    }

    public Vector3 GetGrenadeThrowDestination()
    {
        var grenadeThrowRay = new Ray(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward);
        var position = grenadeThrowRay.GetPoint(10f);
        if (Physics.Raycast(grenadeThrowRay, out var grenadeHit, 12f, throwRaycastMask, QueryTriggerInteraction.Ignore))
            position = grenadeThrowRay.GetPoint(grenadeHit.distance - 0.05f);

        grenadeThrowRay = new Ray(position, Vector3.down);
        if (Physics.Raycast(grenadeThrowRay, out grenadeHit, 30f, throwRaycastMask, QueryTriggerInteraction.Ignore))
            return grenadeHit.point + Vector3.up * 0.05f;
        return grenadeThrowRay.GetPoint(30f);
    }
}
