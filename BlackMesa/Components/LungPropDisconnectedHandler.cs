using UnityEngine;
using UnityEngine.Events;

namespace BlackMesa.Components;

public class LungPropDisconnectedHandler : MonoBehaviour
{
    public UnityEvent Event = new();

    internal void TriggerEvent()
    {
        Event.Invoke();
    }

    internal static void TriggerAllEvents()
    {
        foreach (var handler in FindObjectsByType<LungPropDisconnectedHandler>(FindObjectsSortMode.None))
            handler.TriggerEvent();
    }
}
