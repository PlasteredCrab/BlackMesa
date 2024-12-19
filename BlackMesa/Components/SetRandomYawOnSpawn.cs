using BlackMesa.Utilities;
using UnityEngine;

namespace BlackMesa.Components;

public class SetRandomYawOnSpawn : MonoBehaviour
{
    private void Start()
    {
        var random = new System.Random(StartOfRound.Instance.randomMapSeed + transform.position.IntHash());
        transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
    }
}
