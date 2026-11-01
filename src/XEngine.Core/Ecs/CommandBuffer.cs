using XEngine.Core.Hierarchy;

namespace XEngine.Core.Ecs;

public sealed class CommandBuffer
{
    private readonly World _world;
    private readonly List<EntityCommands> _groups = [];
    private readonly Dictionary<int, EntityCommands> _byIndex = [];

    internal CommandBuffer(World world) { _world = world; }

    public EntityId Create()
    {
        EntityId entity = _world.ReserveEntity();
        EntityCommands group = GetOrCreate(entity);
        group.IsCreated = true;
        group.Destroyed = false;
        return entity;
    }

    public void Add<T>(EntityId entity, T component) where T : struct, IComponent
    {
        if (typeof(T) == typeof(Node)) throw new InvalidOperationException("Node is owned by the world and cannot be added manually.");
        EntityCommands group = GetOrCreate(entity);
        var typeId = ComponentType<T>.Id;
        group.Additions[typeId] = new CommandComponent<T>(component);
        group.Removals.Remove(typeId);
    }

    public void Remove<T>(EntityId entity) where T : struct, IComponent
    {
        if (typeof(T) == typeof(Node)) throw new InvalidOperationException("Node is owned by the world and cannot be removed.");
        EntityCommands group = GetOrCreate(entity);
        var typeId = ComponentType<T>.Id;
        group.Removals.Add(typeId);
        group.Additions.Remove(typeId);
    }

    public void Destroy(EntityId entity, DestroyMode mode = DestroyMode.ReparentChildren, bool keepWorld = false)
    {
        EntityCommands group = GetOrCreate(entity);
        group.Destroyed = true;
        group.DestroyMode = mode;
        group.DestroyKeepWorld = keepWorld;
    }

    public void DestroyIf(bool condition, EntityId entity, DestroyMode mode = DestroyMode.ReparentChildren, bool keepWorld = false)
    {
        if (condition) Destroy(entity, mode, keepWorld);
    }

    public void SetParent(EntityId child, EntityId parent, bool keepWorld = false)
    {
        EntityCommands group = GetOrCreate(child);
        group.HasSetParent = true;
        group.Parent = parent;
        group.ParentKeepWorld = keepWorld;
    }

    internal void Flush(World world)
    {
        if (!ReferenceEquals(world, _world)) throw new InvalidOperationException("Command buffer belongs to another world.");
        foreach (EntityCommands group in _groups)
        {
            if (group.Destroyed) continue;
            EntityId entity = group.Entity;
            if (group.IsCreated)
            {
                ComponentMask mask = ComponentMask.Empty.With(ComponentType<Node>.Id);
                foreach (var typeId in group.Removals) mask = mask.Without(typeId);
                foreach (ICommandComponent addition in group.Additions.Values) mask = mask.With(addition.TypeId);
                world.ActivateReserved(entity, mask);
            }
            else world.ApplyBatch(entity, group.Additions.Values, group.Removals);
            foreach (ICommandComponent addition in group.Additions.Values) addition.Apply(world, entity);
        }
        foreach (EntityCommands group in _groups)
            if (!group.Destroyed && group.HasSetParent)
                world.SetParent(group.Entity, group.Parent, group.ParentKeepWorld);

        foreach (EntityCommands group in _groups)
        {
            if (!group.Destroyed) continue;
            if (group.IsCreated) world.CancelReserved(group.Entity);
            else world.Destroy(group.Entity, group.DestroyMode, group.DestroyKeepWorld);
        }
        _groups.Clear();
        _byIndex.Clear();
    }

    private EntityCommands GetOrCreate(EntityId entity)
    {
        if (_byIndex.TryGetValue(entity.Index, out EntityCommands? group)) return group;
        group = new EntityCommands(entity);
        _byIndex.Add(entity.Index, group);
        _groups.Add(group);
        return group;
    }

    private sealed class EntityCommands(EntityId entity)
    {
        public EntityId Entity { get; } = entity;
        public bool IsCreated { get; set; }
        public bool Destroyed { get; set; }
        public DestroyMode DestroyMode { get; set; }
        public bool DestroyKeepWorld { get; set; }
        public bool HasSetParent { get; set; }
        public EntityId Parent { get; set; }
        public bool ParentKeepWorld { get; set; }
        public Dictionary<int, ICommandComponent> Additions { get; } = [];
        public HashSet<int> Removals { get; } = [];
    }
}
