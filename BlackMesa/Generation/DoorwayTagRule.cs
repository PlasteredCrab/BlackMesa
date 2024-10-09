using DunGen;
using System;

namespace BlackMesa.Generation;

[Serializable]
public struct DoorwayTagRule : IEquatable<DoorwayTagPair>
{
    public DoorwayTag tagA;
    public DoorwayTag tagB;
    public TileConnectionRule.ConnectionResult result;

    public DoorwayTagPair Pair => new DoorwayTagPair(tagA, tagB);

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
