namespace XEngine.Core.Ecs;

internal sealed class EntityStore
{
    private const int MinimumCapacity = 16;

    private EntityRecord[] _records = [];
    private readonly Stack<int> _free = new();
    private int _used;

    public int AliveCount { get; private set; }
    public int Capacity => _records.Length;

    public EntityId Create()
    {
        EntityId id = Allocate();
        _records[id.Index].Alive = true;
        AliveCount++;
        return id;
    }

    public EntityId Reserve() => Allocate();

    public bool IsAlive(EntityId id) => TryGetSlot(id, out var slot) && _records[slot].Alive;

    public bool TryGetSlot(EntityId id, out int slot)
    {
        if ((uint)id.Index >= (uint)_records.Length)
        {
            slot = -1;
            return false;
        }
        if (_records[id.Index].Version != id.Version || !_records[id.Index].Alive)
        {
            slot = -1;
            return false;
        }
        slot = id.Index;
        return true;
    }

    public ref EntityRecord GetRecord(int slot) => ref _records[slot];

    public void Activate(EntityId id)
    {
        ref EntityRecord record = ref _records[id.Index];
        if (record.Alive) throw new InvalidOperationException($"Entity {id} is already alive.");
        record.Alive = true;
        AliveCount++;
    }

    public void ReleaseReserved(EntityId id)
    {
        ref EntityRecord record = ref _records[id.Index];
        if (record.Alive) throw new InvalidOperationException($"Entity {id} is alive and cannot be released as reserved.");
        record.Archetype = null;
        record.Row = -1;
        _free.Push(id.Index);
    }

    public void Destroy(EntityId id)
    {
        ref EntityRecord record = ref _records[id.Index];
        record.Alive = false;
        record.Archetype = null;
        record.Row = -1;
        _free.Push(id.Index);
        AliveCount--;
    }

    private EntityId Allocate()
    {
        int slot;
        if (_free.Count > 0)
        {
            slot = _free.Pop();
            _records[slot].Version++;
        }
        else
        {
            if (_used == _records.Length) Array.Resize(ref _records, _records.Length == 0 ? MinimumCapacity : _records.Length * 2);
            slot = _used++;
            _records[slot].Version = 1;
        }
        return new EntityId(slot, _records[slot].Version);
    }
}
