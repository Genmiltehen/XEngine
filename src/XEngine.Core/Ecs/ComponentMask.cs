using System.Numerics;

namespace XEngine.Core.Ecs;

public readonly struct ComponentMask : IEquatable<ComponentMask>
{
    public const int Capacity = 256;
    private const int WordCount = 4;
    private readonly ulong _word0;
    private readonly ulong _word1;
    private readonly ulong _word2;
    private readonly ulong _word3;

    private ComponentMask(ulong word0, ulong word1, ulong word2, ulong word3)
    {
        _word0 = word0;
        _word1 = word1;
        _word2 = word2;
        _word3 = word3;
    }

    public static ComponentMask Empty => default;
    public bool IsEmpty => (_word0 | _word1 | _word2 | _word3) == 0;

    public int Count =>
        BitOperations.PopCount(_word0) +
        BitOperations.PopCount(_word1) +
        BitOperations.PopCount(_word2) +
        BitOperations.PopCount(_word3);

    public bool Has(int componentTypeId)
    {
        ValidateId(componentTypeId);
        return (GetWord(componentTypeId >> 6) & GetBit(componentTypeId)) != 0;
    }

    public ComponentMask With(int componentTypeId)
    {
        ValidateId(componentTypeId);
        var wordIndex = componentTypeId >> 6;
        return WithWord(wordIndex, GetWord(wordIndex) | GetBit(componentTypeId));
    }

    public ComponentMask Without(int componentTypeId)
    {
        ValidateId(componentTypeId);
        var wordIndex = componentTypeId >> 6;
        return WithWord(wordIndex, GetWord(wordIndex) & ~GetBit(componentTypeId));
    }

    public bool ContainsAll(ComponentMask other) =>
        (_word0 & other._word0) == other._word0 &&
        (_word1 & other._word1) == other._word1 &&
        (_word2 & other._word2) == other._word2 &&
        (_word3 & other._word3) == other._word3;

    public bool Overlaps(ComponentMask other) =>
        (_word0 & other._word0) != 0 ||
        (_word1 & other._word1) != 0 ||
        (_word2 & other._word2) != 0 ||
        (_word3 & other._word3) != 0;

    internal bool Matches(ComponentMask with, ComponentMask without) => ContainsAll(with) && !Overlaps(without);

    public int CopyTypeIds(Span<int> destination)
    {
        if (destination.Length < Count) throw new ArgumentException("Destination is too small for the mask.", nameof(destination));
        var count = 0;
        for (var wordIndex = 0; wordIndex < WordCount; wordIndex++)
            for (var bits = GetWord(wordIndex); bits != 0; bits &= bits - 1)
                destination[count++] = (wordIndex << 6) + BitOperations.TrailingZeroCount(bits);
        return count;
    }
 
    public bool Equals(ComponentMask other) =>
        _word0 == other._word0 &&
        _word1 == other._word1 &&
        _word2 == other._word2 &&
        _word3 == other._word3;

    public override bool Equals(object? obj) => obj is ComponentMask other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_word0, _word1, _word2, _word3);

    public static bool operator ==(ComponentMask left, ComponentMask right) => left.Equals(right);

    public static bool operator !=(ComponentMask left, ComponentMask right) => !left.Equals(right);

    private static ulong GetBit(int componentTypeId) => 1UL << (componentTypeId & 63);

    private static void ValidateId(int componentTypeId)
    {
        if ((uint)componentTypeId >= Capacity) throw new ArgumentOutOfRangeException(nameof(componentTypeId), componentTypeId, $"Component type id must be in range [0, {Capacity - 1}].");
    }

    private ulong GetWord(int wordIndex) => wordIndex switch
    {
        0 => _word0,
        1 => _word1,
        2 => _word2,
        3 => _word3,
        _ => throw new ArgumentOutOfRangeException(nameof(wordIndex)),
    };

    private ComponentMask WithWord(int wordIndex, ulong word) => wordIndex switch
    {
        0 => new ComponentMask(word, _word1, _word2, _word3),
        1 => new ComponentMask(_word0, word, _word2, _word3),
        2 => new ComponentMask(_word0, _word1, word, _word3),
        3 => new ComponentMask(_word0, _word1, _word2, word),
        _ => throw new ArgumentOutOfRangeException(nameof(wordIndex)),
    };
}
