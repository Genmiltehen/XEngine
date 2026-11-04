namespace XEngine.Core.Ecs;

internal sealed class Archetype
{
    private const int MinimumCapacity = 4;

    private readonly ComponentRegistry _registry;
    private readonly int[] _columnIndices;
    private EntityId[] _entities = [];

    public Archetype(ComponentMask mask, ComponentRegistry registry)
    {
        _registry = registry;
        Mask = mask;
        Span<int> ids = stackalloc int[ComponentMask.Capacity];
        var typeCount = mask.CopyTypeIds(ids);
        Types = new ComponentType[typeCount];
        Columns = new IComponentColumn[typeCount];
        _columnIndices = new int[ComponentMask.Capacity];
        Array.Fill(_columnIndices, -1);
        for (var i = 0; i < typeCount; i++)
        {
            ComponentType type = registry.GetById(ids[i]);
            Types[i] = type;
            Columns[i] = type.CreateColumn(MinimumCapacity);
            _columnIndices[ids[i]] = i;
        }
    }

    public ComponentMask Mask { get; }
    public ComponentType[] Types { get; }
    public IComponentColumn[] Columns { get; }
    public EntityId[] Entities => _entities;
    public int Count { get; internal set; }

    public int IndexOf(int componentTypeId) => _columnIndices[componentTypeId];

    public Column<T> GetColumn<T>() where T : struct, IComponent => GetColumn<T>(_registry.Get<T>());

    internal Column<T> GetColumn<T>(ComponentType type) where T : struct, IComponent => (Column<T>)Columns[_columnIndices[type.Id]];

    public int AddEntity(EntityId entity)
    {
        var row = Count;
        EnsureEntityCapacity(row + 1);
        _entities[row] = entity;
        foreach (IComponentColumn column in Columns) column.EnsureCount(row + 1);
        Count = row + 1;
        return row;
    }

    private void EnsureEntityCapacity(int capacity)
    {
        if (capacity <= _entities.Length) return;
        var newCapacity = _entities.Length == 0 ? MinimumCapacity : _entities.Length;
        while (newCapacity < capacity) newCapacity = checked(newCapacity * 2);
        Array.Resize(ref _entities, newCapacity);
    }
}
