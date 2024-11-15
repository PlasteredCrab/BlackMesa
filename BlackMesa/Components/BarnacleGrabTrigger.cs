using BlackMesa.Components;
using GameNetcodeStuff;
using UnityEngine;

public class BarnacleGrabTrigger : MonoBehaviour
{
    public Barnacle barnacle;

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent<PlayerControllerB>(out var player))
            barnacle.TryGrabPlayerOrHeldItem(player);
        if (other.TryGetComponent<GrabbableObject>(out var item))
            barnacle.GrabItem(item);
    }
}
