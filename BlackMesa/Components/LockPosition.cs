using UnityEngine;

namespace BlackMesa.Components;

public class LockPosition : MonoBehaviour
{
    internal Transform target;

    private void LateUpdate()
    {
        transform.position = target.position;
        transform.rotation = target.rotation;
    }
}
