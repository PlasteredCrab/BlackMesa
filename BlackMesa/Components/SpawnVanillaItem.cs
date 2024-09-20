using UnityEngine;

namespace BlackMesa.Components;

internal class SpawnVanillaItem : MonoBehaviour
{
    public string itemName;

    private void Awake()
    {
        var item = GetItemByName(itemName);

        if (item == null)
            return;

        if (GetComponent<SpawnSyncedObject>() is not { } spawner)
            spawner = gameObject.AddComponent<SpawnSyncedObject>();
        spawner.spawnPrefab = item.spawnPrefab;
    }

    private Item GetItemByName(string name)
    {
        foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
        {
            if (item.itemName == name)
                return item;
        }

        return null;
    }
}
