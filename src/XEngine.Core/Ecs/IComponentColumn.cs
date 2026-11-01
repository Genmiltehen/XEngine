namespace XEngine.Core.Ecs;

internal interface IComponentColumn
{
    int Count { get; }

    void EnsureCapacity(int capacity);

    void EnsureCount(int count);

    void CopyTo(int sourceIndex, IComponentColumn destination, int destinationIndex);

    void Reset(int index);

    void RemoveAtSwapBack(int index);

    void Clear();
}
