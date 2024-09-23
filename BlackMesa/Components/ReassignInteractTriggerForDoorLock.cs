using UnityEngine;

namespace BlackMesa.Components;

public class ReassignInteractTriggerForDoorLock : MonoBehaviour
{
    public DoorLock doorToTrigger;
    public InteractTrigger interactTrigger;

    private void Start()
    {
        doorToTrigger.doorTrigger = interactTrigger;
    }
}
