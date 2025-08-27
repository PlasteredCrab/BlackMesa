using System;
using PathfindingLib.API.SmartPathfinding;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components;

public class ElevatorController : NetworkBehaviour, IElevator
{
    public enum Position
    {
        Bottom = 0,
        Top = 1,
    }

    public Animator animator;
    public PlayerPhysicsRegion physicsRegion;

    public AudioSource elevatorAudio;
    public AudioClip elevatorTravelSFX;
    public AudioClip elevatorReachTopSFX;
    public AudioClip elevatorReachBottomSFX;

    public AudioSource jingleSource;
    public float jingleFadeTime;
    public float jingleVolume = 1;

    public AudioSource dingSource;

    public Transform insideNavPoint;
    public Transform bottomNavPoint;
    public Transform topNavPoint;

    public Collider insideCollider;

    internal Position targetFloor = Position.Bottom;
    internal bool doorsOpen = true;

    private ElevatorFloor bottomFloor;
    private ElevatorFloor topFloor;

    private void CreateFloors()
    {
        if (bottomFloor != null && topFloor != null)
            return;
        bottomFloor = new(this, bottomNavPoint);
        topFloor = new(this, topNavPoint);
    }

    private void OnEnable()
    {
        CreateFloors();
        SmartPathfinding.RegisterElevatorFloor(bottomFloor);
        SmartPathfinding.RegisterElevatorFloor(topFloor);
    }

    private void OnDisable()
    {
        SmartPathfinding.UnregisterElevatorFloor(bottomFloor);
        SmartPathfinding.UnregisterElevatorFloor(topFloor);
    }

    public void CallElevator(Position position)
    {
        if (!IsOwner)
            return;
        CallElevatorServerRpc(position);
    }

    [ServerRpc]
    private void CallElevatorServerRpc(Position position)
    {
        CallElevatorClientRpc(position);
    }

    [ClientRpc]
    private void CallElevatorClientRpc(Position position)
    {
        animator.SetInteger("Floor", (int)position);
    }

    public void SetTargetFloor(Position position)
    {
        doorsOpen = false;
        targetFloor = position;

        jingleSource.Play();
    }

    public void SetDoorsOpen()
    {
        doorsOpen = true;
    }

    public void ShakeCamera(ScreenShakeType type)
    {
        if (StartOfRound.Instance.localPlayerController.physicsParent != physicsRegion.physicsTransform)
            return;
        HUDManager.Instance.ShakeCamera(type);
    }

    public void PlayElevatorTravelSFX()
    {
        elevatorAudio.PlayOneShot(elevatorTravelSFX);
    }

    public void PlayElevatorReachTopSFX()
    {
        elevatorAudio.PlayOneShot(elevatorReachTopSFX);
    }

    public void PlayElevatorReachBottomSFX()
    {
        elevatorAudio.PlayOneShot(elevatorReachBottomSFX);
    }

    public void PlayDingSFX()
    {
        dingSource.Play();
    }

    private void Update()
    {
        if (jingleSource.isPlaying)
        {
            var volumeDeltaSign = doorsOpen ? -1 : 1;
            var volume = jingleSource.volume;
            volume += volumeDeltaSign * Time.deltaTime / jingleFadeTime / jingleVolume;

            jingleSource.volume = Mathf.Clamp(volume, 0, jingleVolume);

            if (jingleSource.volume <= 0)
                jingleSource.Stop();
        }
    }

    public Transform InsideButtonNavMeshNode => insideNavPoint;

    private ElevatorFloor GetClosestFloor()
    {
        var insidePoint = insideNavPoint.position;
        var distanceBottom = (bottomNavPoint.position - insidePoint).sqrMagnitude;
        var distanceTop = (topNavPoint.position - insidePoint).sqrMagnitude;
        if (distanceBottom < distanceTop)
            return bottomFloor;
        return topFloor;
    }

    public ElevatorFloor ClosestFloor => GetClosestFloor();

    public bool DoorsAreOpen => doorsOpen;

    public ElevatorFloor TargetFloor => targetFloor == Position.Bottom ? bottomFloor : topFloor;


    const int goDownAnimTime = 265;
    const int goUpAnimTime = 303;
    const int avgTransitAnimTime = 253;
    const float speed = 0.4f;

    static float GetAnimationSeconds(int animTime)
    {
        return animTime / 60f / speed;
    }

    public float TimeToCompleteCurrentMovement()
    {
        var topY = topNavPoint.position.y;
        var bottomY = bottomNavPoint.position.y;

        var totalDistance = Math.Abs(topY - bottomY);

        var targetY = targetFloor == Position.Bottom ? bottomY : topY;
        var currentDistance = Math.Abs(insideNavPoint.position.y - targetY);

        return GetAnimationSeconds(avgTransitAnimTime) * currentDistance / totalDistance;
    }

    public float TimeFromFloorToFloor(ElevatorFloor a, ElevatorFloor b)
    {
        if (a == b)
            return 0;
        if (a == bottomFloor)
            return GetAnimationSeconds(goUpAnimTime);
        return GetAnimationSeconds(goDownAnimTime);
    }

    public void GoToFloor(ElevatorFloor floor)
    {
        CallElevator(floor == bottomFloor ? Position.Bottom : Position.Top);
    }

    public bool IsInsideElevator(Vector3 point)
    {
        return insideCollider.ClosestPoint(point) == point;
    }
}
