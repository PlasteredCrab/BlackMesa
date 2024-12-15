using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Events;

namespace BlackMesa.Components;

public class PlayerTriggerRegion : MonoBehaviour
{
    public UnityEvent<PlayerControllerB> onStay;

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerControllerB player))
            onStay.Invoke(player);
    }
}
