using DunGen;
using System;

namespace BlackMesa.Generation;

public struct DoorwayTagPair(DoorwayTag tagA, DoorwayTag tagB) : IEquatable<DoorwayTagPair>
{
    public DoorwayTag tagA = tagA;
    public DoorwayTag tagB = tagB;

    public DoorwayTagPair(Doorway doorwayA, Doorway doorwayB)
        : this(doorwayA.TryGetComponent<DoorwayInfo>(out var info) ? info.doorwayTag : null,
              doorwayB.TryGetComponent(out info) ? info.doorwayTag : null)
    { }

    public readonly bool Equals(DoorwayTagPair other)
    {
        return tagA == other.tagA && tagB == other.tagB;
    }

    public override readonly bool Equals(object other)
    {
        if (other is DoorwayTagPair otherPair)
            return Equals(otherPair);
        return false;
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(tagA.GetHashCode(), tagB.GetHashCode());
    }
}
