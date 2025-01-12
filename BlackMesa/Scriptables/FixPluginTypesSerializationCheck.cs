using System;
using UnityEngine;

namespace BlackMesa.Scriptables;

[CreateAssetMenu(menuName = "Black Mesa/FixPluginTypesSerialization Check")]
public class FixPluginTypesSerializationCheck : ScriptableObject
{
    public SerializableClass buh;

    public bool IsWorking => buh?.IsWorking ?? false;
}

[Serializable]
public class SerializableClass
{
    public bool IsWorking = false;
}
