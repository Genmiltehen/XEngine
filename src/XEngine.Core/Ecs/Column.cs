namespace XEngine.Core.Ecs;

internal sealed class Column<T>(int capacity) : IComponentColumn where T : struct, IComponent
{
    private const int MinimumCapacity = 4;
    private T[] _items = capacity > 0 ? new T[capacity] : [];

    public int Count { get; private set; }

    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return ref _items[index];
        }
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity <= _items.Length) return;
        var newCapacity = _items.Length == 0 ? MinimumCapacity : _items.Length;
        while (newCapacity < capacity) newCapacity = checked(newCapacity * 2);
        Array.Resize(ref _items, newCapacity);
    }

    public void EnsureCount(int count)
    {
        if (count <= Count) return;
        EnsureCapacity(count);
        Array.Clear(_items, Count, count - Count);
        Count = count;
    }

    public void CopyTo(int sourceIndex, IComponentColumn destination, int destinationIndex)
    {
        if (destination is not Column<T> target) throw new InvalidOperationException("Component column types do not match.");
        target._items[destinationIndex] = _items[sourceIndex];
    }

    public void Reset(int index) => _items[index] = default;

    public void RemoveAtSwapBack(int index)
    {
        var last = Count - 1;
        if (index != last) _items[index] = _items[last];
        _items[last] = default;
        Count = last;
    }

    public void Clear()
    {
        Array.Clear(_items, 0, Count);
        Count = 0;
    }
}
