namespace XEngine.Core.Ecs;

public abstract class ComponentType
{
    private static readonly ComponentType?[] ById = new ComponentType?[ComponentMask.Capacity];
    private static readonly Lock RegistryLock = new();
    private static int _nextId;

    protected ComponentType(Type type)
    {
        Type = type;
        Name = type.Name;
        lock (RegistryLock)
        {
            if (_nextId >= ComponentMask.Capacity) throw new InvalidOperationException($"Component type limit exceeded ({ComponentMask.Capacity}).");
            Id = _nextId;
            ById[Id] = this;
            _nextId++;
        }
    }

    public int Id { get; }
    public Type Type { get; }
    public string Name { get; }

    internal abstract IComponentColumn CreateColumn(int capacity);

    internal static ComponentType GetById(int id)
    {
        lock (RegistryLock) return (uint)id >= (uint)ById.Length || ById[id] is null
                ? throw new ArgumentOutOfRangeException(nameof(id), id, "Component type is not registered.")
                : ById[id]!;
    }
}
