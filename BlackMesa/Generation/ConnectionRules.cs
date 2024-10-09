using DunGen;
using System.Collections.Generic;
using UnityEngine;

namespace BlackMesa.Generation;

public class ConnectionRules : MonoBehaviour
{
    public DoorwayTagRule[] doorwayTagRules;

    private TileConnectionRule rule;
    private Dictionary<DoorwayTagPair, TileConnectionRule.ConnectionResult> doorwayTagRuleLookup = [];

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }

    private void OnEnable()
    {
        rule = new TileConnectionRule(CanTilesConnect);
        doorwayTagRuleLookup.Clear();
        foreach (var doorwayTagRule in doorwayTagRules)
            doorwayTagRuleLookup.Add(doorwayTagRule.Pair, doorwayTagRule.result);
        DoorwayPairFinder.CustomConnectionRules.Add(rule);
    }

    private void OnDisable()
    {
        DoorwayPairFinder.CustomConnectionRules.Remove(rule);
        rule = null;
    }

    private TileConnectionRule.ConnectionResult CanTilesConnect(Tile tileA, Tile tileB, Doorway doorwayA, Doorway doorwayB)
    {
        if (doorwayTagRuleLookup.TryGetValue(new DoorwayTagPair(doorwayA, doorwayB), out var result))
            return result;

        return TileConnectionRule.ConnectionResult.Passthrough;
    }
}
