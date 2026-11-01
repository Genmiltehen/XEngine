using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using XEngine.Core.Hierarchy;
using XEngine.Core.Systems;
using XEngine.Core.Time;
using XEngine.Core.Transforms;

namespace XEngine.Core.Ecs;

public sealed class World : IDisposable
{
    private static readonly ComponentMask NodeMask = ComponentMask.Empty.With(ComponentType<Node>.Id);

    // Хранилище и системы
    private readonly EntityStore _entities = new();
    private readonly Dictionary<ComponentMask, Archetype> _archetypes = [];
    private readonly List<Archetype> _archetypeList = [];
    private readonly Dictionary<Type, object> _resources = [];
    private readonly SystemScheduler _systems = new();

    // Состояние кадра
    private int _iterationDepth;
    private bool _isUpdating;
    private bool _disposed;

    public World(WorldConfigurator? configurator = null)
    {
        (configurator ?? WorldConfigurators.Default)(this);
    }

    public int EntityCount => _entities.AliveCount;
    public int ArchetypeCount => _archetypes.Count;
    public GameTime Time { get; private set; }

#region Entity Manipulation
    public bool IsAlive(EntityId entity) => _entities.IsAlive(entity);

    public EntityId Create()
    {
        EnsureNotIterating();
        EntityId entity = _entities.Create();
        PlaceEntity(entity, NodeMask);
        return entity;
    }

    public void Destroy(EntityId entity, DestroyMode mode = DestroyMode.ReparentChildren, bool keepWorld = false)
    {
        EnsureNotIterating();
        if (!_entities.IsAlive(entity))
        {
            Debug.Assert(false, $"Attempted to destroy dead entity {entity}.");
            return;
        }
        if (mode == DestroyMode.Recursive)
        {
            DestroySubtree(entity);
            return;
        }
        EntityId parent = GetNodeRef(entity).Parent;
        foreach (EntityId child in Children(entity)) SetParent(child, parent, keepWorld);
        UnlinkFromParent(entity);
        RemoveEntity(entity);
    }

    public void SetParent(EntityId child, EntityId parent, bool keepWorld = false)
    {
        EnsureNotIterating();
        _ = GetAliveRecord(child);
        if (!parent.IsNone) _ = GetAliveRecord(parent);
        if (child == parent) throw new InvalidOperationException("An entity cannot be its own parent.");
        if (!parent.IsNone && IsDescendantOrSelf(parent, child)) throw new InvalidOperationException("Reparenting would create a cycle in the hierarchy.");
        if (GetNodeRef(child).Parent == parent) return;
        
        var preserveWorld = keepWorld && Has<Transform2D>(child);
        Matrix3x2 oldWorld = default;
        Matrix3x2 parentWorld = default;
        if (preserveWorld)
        {
            oldWorld = ComputeWorldMatrix(child);
            parentWorld = parent.IsNone ? Matrix3x2.Identity : ComputeWorldMatrix(parent);
        }

        UnlinkFromParent(child);
        if (parent.IsNone)
        {
            ref Node root = ref GetNodeRef(child);
            root.Parent = EntityId.None;
            root.PrevSibling = EntityId.None;
            root.NextSibling = EntityId.None;
            SetSubtreeDepth(child, 0);
            UpdateActive(child, parentActive: true);
        }
        else
        {
            AppendChild(parent, child);
            ref Node parentNode = ref GetNodeRef(parent);
            SetSubtreeDepth(child, parentNode.Depth + 1);
            UpdateActive(child, parentNode.ActiveInHierarchy);
        }
        
        if (!preserveWorld) return;
        if (!Matrix3x2.Invert(parentWorld, out Matrix3x2 inverseParent)) throw new InvalidOperationException("Cannot preserve world transform because the new parent matrix is not invertible.");
        
        Matrix3x2 localWorld = oldWorld * inverseParent;
        Decompose(localWorld, out Vector2 position, out var rotation, out Vector2 scale);
        ref Transform2D transform = ref Get<Transform2D>(child);
        transform.Position = position;
        transform.Rotation = rotation;
        transform.Scale = scale;
        transform.World = oldWorld;
    }

    public void SetActive(EntityId entity, bool active)
    {
        EnsureNotIterating();
        ref Node node = ref GetNodeRef(entity);
        if (active) node.Flags |= NodeFlags.ActiveSelf;
        else node.Flags &= ~NodeFlags.ActiveSelf;
        var parentActive = node.Parent.IsNone || GetNodeRef(node.Parent).ActiveInHierarchy;
        UpdateActive(entity, parentActive);
    }

    public ChildEnumerable Children(EntityId entity) => new(this, GetNodeRef(entity).FirstChild);

    public void Add<T>(EntityId entity, T component) where T : struct, IComponent
    {
        if (typeof(T) == typeof(Node)) throw new InvalidOperationException("Node is owned by the world and cannot be added manually.");
        EnsureNotIterating();
        
        ref EntityRecord sourceRecord = ref GetAliveRecord(entity);
        Archetype sourceArch = sourceRecord.Archetype!;
        if (sourceArch.Mask.Has(ComponentType<T>.Id))
        {
            sourceArch.GetColumn<T>()[sourceRecord.Row] = component;
            return;
        }

        ComponentMask targetMask = sourceArch.Mask.With(ComponentType<T>.Id);
        Archetype targetArch = GetOrCreateArchetype(targetMask);
        var targetRow = MoveEntity(entity, sourceArch, sourceRecord.Row, targetArch);
        targetArch.GetColumn<T>()[targetRow] = component;
    }

    public void Remove<T>(EntityId entity) where T : struct, IComponent
    {
        if (typeof(T) == typeof(Node)) throw new InvalidOperationException("Node is owned by the world and cannot be removed.");
        EnsureNotIterating();

        ref EntityRecord sourceRecord = ref GetAliveRecord(entity);
        Archetype sourceArch = sourceRecord.Archetype!;
        if (!sourceArch.Mask.Has(ComponentType<T>.Id)) return;

        ComponentMask targetMask = sourceArch.Mask.Without(ComponentType<T>.Id);
        Archetype targetArch = GetOrCreateArchetype(targetMask);
        _ = MoveEntity(entity, sourceArch, sourceRecord.Row, targetArch);
    }

    public ref T Get<T>(EntityId entity) where T : struct, IComponent
    {
        ref EntityRecord record = ref GetAliveRecord(entity);
        Archetype archetype = record.Archetype!;
        if (!archetype.Mask.Has(ComponentType<T>.Id)) throw new InvalidOperationException($"Entity {entity} does not have component {ComponentType<T>.Instance.Name}.");
        
        return ref archetype.GetColumn<T>()[record.Row];
    }

    public bool TryGet<T>(EntityId entity, out T component) where T : struct, IComponent
    {
        if (!_entities.IsAlive(entity))
        {
            component = default;
            return false;
        }

        ref EntityRecord record = ref _entities.GetRecord(entity.Index);
        Archetype archetype = record.Archetype!;
        if (!archetype.Mask.Has(ComponentType<T>.Id))
        {
            component = default;
            return false;
        }
        
        component = archetype.GetColumn<T>()[record.Row];
        return true;
    }

    public bool Has<T>(EntityId entity) where T : struct, IComponent => _entities.IsAlive(entity) && _entities.GetRecord(entity.Index).Archetype!.Mask.Has(ComponentType<T>.Id);
#endregion

#region CommandBuffer
    public CommandBuffer BeginCommands() => new(this);

    internal EntityId ReserveEntity() => _entities.Reserve();

    internal void CancelReserved(EntityId entity) => _entities.ReleaseReserved(entity);

    internal void ActivateReserved(EntityId entity, ComponentMask components)
    {
        if (!components.Has(ComponentType<Node>.Id)) components = components.With(ComponentType<Node>.Id);
        PlaceEntity(entity, components);
        _entities.Activate(entity);
    }

    internal void ApplyBatch(EntityId entity, IEnumerable<ICommandComponent> additions, IEnumerable<int> removals)
    {
        ref EntityRecord record = ref GetAliveRecord(entity);
        Archetype sourceArch = record.Archetype!;
        ComponentMask mask = sourceArch.Mask;
        foreach (var typeId in removals) mask = mask.Without(typeId);
        foreach (ICommandComponent addition in additions) mask = mask.With(addition.TypeId);
        if (mask == sourceArch.Mask) return;
        Archetype targetArch = GetOrCreateArchetype(mask);
        _ = MoveEntity(entity, sourceArch, record.Row, targetArch);
    }

    internal void SetExisting<T>(EntityId entity, T component) where T : struct, IComponent
    {
        ref EntityRecord record = ref GetAliveRecord(entity);
        Archetype archetype = record.Archetype!;
        if (!archetype.Mask.Has(ComponentType<T>.Id)) throw new InvalidOperationException($"Entity {entity} does not have component {ComponentType<T>.Instance.Name}.");
        archetype.GetColumn<T>()[record.Row] = component;
    }

    public void Submit(CommandBuffer commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        EnsureNotIterating();
        commands.Flush(this);
    }
#endregion

    internal ArchetypeFilter FilterArchetypes(ComponentMask with, ComponentMask without) => new(_archetypeList, with, without);

    internal void BeginIteration() => _iterationDepth++;
    internal void EndIteration() => _iterationDepth--;

#region Queries
    public Query<T1> Query<T1>()
        where T1 : struct, IComponent => new(this, ComponentMask.Empty.With(ComponentType<T1>.Id));

    public Query<T1, T2> Query<T1, T2>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent => new(this, ComponentMask.Empty.With(ComponentType<T1>.Id).With(ComponentType<T2>.Id));

    public Query<T1, T2, T3> Query<T1, T2, T3>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent => new(this, ComponentMask.Empty.With(ComponentType<T1>.Id).With(ComponentType<T2>.Id).With(ComponentType<T3>.Id));
#endregion

#region System Manipulation
    public void AddSystem(SystemPhase phase, ISystem system)
    {
        ThrowIfDisposed();
        _systems.Add(phase, system);
    }

    public void Update(float dt)
    {
        ThrowIfDisposed();
        EnsureNotIterating();
        if (_isUpdating) throw new InvalidOperationException("World.Update cannot be called recursively.");

        Time = new GameTime(dt, Time.TotalTime + dt, Time.FrameCount + 1);
        _isUpdating = true;
        try { _systems.RunAll(this, dt); }
        finally { _isUpdating = false; }
    }

    public void UpdateTransforms(float dt = 0f)
    {
        ThrowIfDisposed();
        EnsureNotIterating();
        _systems.Run(SystemPhase.Transform, this, dt);
    }
#endregion
    
#region Resource Manipulation
        public T GetResource<T>() where T : class
        {
            ThrowIfDisposed();
            return _resources.TryGetValue(typeof(T), out var resource)
                ? (T)resource
                : throw new InvalidOperationException($"Resource {typeof(T).Name} is not registered.");
        }
    
        public bool TryGetResource<T>([NotNullWhen(true)] out T? resource) where T : class
        {
            ThrowIfDisposed();
            if (_resources.TryGetValue(typeof(T), out var stored))
            {
                resource = (T)stored;
                return true;
            }
            resource = null;
            return false;
        }
    
        public void SetResource<T>(T resource) where T : class
        {
            ArgumentNullException.ThrowIfNull(resource);
            ThrowIfDisposed();
            if (_resources.TryGetValue(typeof(T), out var previous))
            {
                _resources[typeof(T)] = resource;
                if (!ReferenceEquals(previous, resource)) DisposeIfNeeded(previous);
                return;
            }
            _resources.Add(typeof(T), resource);
        }
    
        public bool RemoveResource<T>() where T : class
        {
            ThrowIfDisposed();
            if (!_resources.Remove(typeof(T), out var resource)) return false;
            DisposeIfNeeded(resource);
            return true;
        }
#endregion

    private ref Node GetNodeRef(EntityId entity)
    {
        ref EntityRecord record = ref GetAliveRecord(entity);
        return ref record.Archetype!.GetColumn<Node>()[record.Row];
    }

    private bool IsDescendantOrSelf(EntityId candidate, EntityId ancestor)
    {
        EntityId current = candidate;
        while (!current.IsNone)
        {
            if (current == ancestor) return true;
            current = GetNodeRef(current).Parent;
        }
        return false;
    }

    private void AppendChild(EntityId parent, EntityId child)
    {
        ref Node parentNode = ref GetNodeRef(parent);
        ref Node childNode = ref GetNodeRef(child);
        childNode.Parent = parent;
        childNode.PrevSibling = parentNode.LastChild;
        childNode.NextSibling = EntityId.None;
        if (parentNode.LastChild.IsNone) parentNode.FirstChild = child;
        else GetNodeRef(parentNode.LastChild).NextSibling = child;
        parentNode.LastChild = child;
    }

    private void UnlinkFromParent(EntityId child)
    {
        ref Node childNode = ref GetNodeRef(child);
        EntityId parent = childNode.Parent;

        if (parent.IsNone)
        {
            childNode.PrevSibling = EntityId.None;
            childNode.NextSibling = EntityId.None;
            return;
        }
        
        ref Node parentNode = ref GetNodeRef(parent);
        EntityId previous = childNode.PrevSibling;
        EntityId next = childNode.NextSibling;
        
        if (previous.IsNone) parentNode.FirstChild = next;
        else GetNodeRef(previous).NextSibling = next;
        if (next.IsNone) parentNode.LastChild = previous;
        else GetNodeRef(next).PrevSibling = previous;

        childNode.Parent = EntityId.None;
        childNode.PrevSibling = EntityId.None;
        childNode.NextSibling = EntityId.None;
    }

    private void SetSubtreeDepth(EntityId entity, int depth)
    {
        GetNodeRef(entity).Depth = depth;
        foreach (EntityId child in Children(entity)) SetSubtreeDepth(child, depth + 1);
    }

    private void UpdateActive(EntityId entity, bool parentActive)
    {
        ref Node node = ref GetNodeRef(entity);
        var active = parentActive && node.ActiveSelf;
        if (active) node.Flags |= NodeFlags.ActiveInHierarchy;
        else node.Flags &= ~NodeFlags.ActiveInHierarchy;
        foreach (EntityId child in Children(entity)) UpdateActive(child, active);
    }

    private void DestroySubtree(EntityId root)
    {
        EntityId parent = GetNodeRef(root).Parent;
        List<EntityId> entities = [];
        CollectSubtree(root, entities);
        if (!parent.IsNone) UnlinkFromParent(root);
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            EntityId entity = entities[i];
            if (_entities.IsAlive(entity)) RemoveEntity(entity);
        }
    }

    private void CollectSubtree(EntityId entity, List<EntityId> destination)
    {
        destination.Add(entity);
        foreach (EntityId child in Children(entity)) CollectSubtree(child, destination);
    }

    private void RemoveEntity(EntityId entity)
    {
        ref EntityRecord record = ref _entities.GetRecord(entity.Index);
        RemoveRow(record.Archetype!, record.Row);
        _entities.Destroy(entity);
    }

    // Считаем от локальных значений: кеш World при переподвешивании ещё старый.
    internal Matrix3x2 ComputeWorldMatrix(EntityId entity)
    {
        Matrix3x2 world = Matrix3x2.Identity;
        EntityId current = entity;
        while (!current.IsNone)
        {
            if (TryGet(current, out Transform2D local)) world *= local.LocalMatrix;
            current = GetNodeRef(current).Parent;
        }
        return world;
    }

    private static void Decompose(Matrix3x2 matrix, out Vector2 position, out float rotation, out Vector2 scale)
    {
        position = new Vector2(matrix.M31, matrix.M32);
        var scaleX = MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12));
        if (scaleX > 1e-6f)
        {
            rotation = MathF.Atan2(matrix.M12, matrix.M11);
            var scaleY = ((matrix.M11 * matrix.M22) - (matrix.M12 * matrix.M21)) / scaleX;
            scale = new Vector2(scaleX, scaleY);
            return;
        }
        var fallbackScaleY = MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22));
        rotation = MathF.Atan2(-matrix.M21, matrix.M22);
        scale = new Vector2(0f, fallbackScaleY);
    }

    private void PlaceEntity(EntityId entity, ComponentMask components)
    {
        Archetype archetype = GetOrCreateArchetype(components);
        var row = archetype.AddEntity(entity);
        if (components.Has(ComponentType<Node>.Id)) archetype.GetColumn<Node>()[row] = Node.New();
        ref EntityRecord record = ref _entities.GetRecord(entity.Index);
        record.Archetype = archetype;
        record.Row = row;
    }

    private int MoveEntity(EntityId entity, Archetype sourceArch, int sourceRow, Archetype targetArch)
    {
        var targetRow = targetArch.AddEntity(entity);
        for (var columnIter = 0; columnIter < targetArch.Types.Length; columnIter++)
        {
            var sourceColumn = sourceArch.IndexOf(targetArch.Types[columnIter].Id);
            if (sourceColumn >= 0) sourceArch.Columns[sourceColumn].CopyTo(sourceRow, targetArch.Columns[columnIter], targetRow);
        }
        RemoveRow(sourceArch, sourceRow);
        ref EntityRecord record = ref _entities.GetRecord(entity.Index);
        record.Archetype = targetArch;
        record.Row = targetRow;
        return targetRow;
    }

    private void RemoveRow(Archetype archetype, int row)
    {
        var last = archetype.Count - 1;
        EntityId prevLastEntity = archetype.Entities[last];
        for (var column = 0; column < archetype.Columns.Length; column++) archetype.Columns[column].RemoveAtSwapBack(row);
        if (row != last)
        {
            archetype.Entities[row] = prevLastEntity;
            ref EntityRecord movedRecord = ref _entities.GetRecord(prevLastEntity.Index);
            movedRecord.Row = row;
        }
        archetype.Entities[last] = EntityId.None;
        archetype.Count = last;
    }

    private Archetype GetOrCreateArchetype(ComponentMask mask)
    {
        if (_archetypes.TryGetValue(mask, out Archetype? archetype)) return archetype;
        archetype = new Archetype(mask);
        _archetypes.Add(mask, archetype);
        _archetypeList.Add(archetype);
        return archetype;
    }

    [Conditional("DEBUG")]
    private void EnsureNotIterating()
    {
        if (_iterationDepth > 0) throw new InvalidOperationException("Structural changes are not allowed during query iteration. Use a CommandBuffer.");
    }

    private ref EntityRecord GetAliveRecord(EntityId entity)
    {
        if (_entities.TryGetSlot(entity, out var slot)) return ref _entities.GetRecord(slot);
        throw new InvalidOperationException($"Entity {entity} is not alive.");
    }

    private static void DisposeIfNeeded(object? resource)
    {
        if (resource is IDisposable disposable) disposable.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _systems.Dispose();
        foreach (var resource in _resources.Values) DisposeIfNeeded(resource);
        _resources.Clear();
        _archetypes.Clear();
        _archetypeList.Clear();
    }
}
