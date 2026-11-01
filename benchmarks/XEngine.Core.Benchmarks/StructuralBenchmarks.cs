using BenchmarkDotNet.Attributes;
using XEngine.Core.Ecs;

namespace XEngine.Core.Benchmarks;

[MemoryDiagnoser]
public class StructuralBenchmarks : IDisposable
{
    private const int Count = 1_000;

    private World _world = null!;
    private EntityId[] _entities = null!;
    private EntityId[] _scratch = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        _entities = new EntityId[Count];
        _scratch = new EntityId[Count];
        for (int i = 0; i < Count; i++)
        {
            EntityId entity = _world.Create();
            _world.Add(entity, new Position { X = i, Y = i });
            _entities[i] = entity;
        }
        // Warm the storage so measured invocations do not include capacity growth.
        for (int i = 0; i < Count; i++) _scratch[i] = _world.Create();
        for (int i = 0; i < Count; i++) _world.Destroy(_scratch[i]);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(OperationsPerInvoke = Count * 2)]
    public void CreateAndDestroy()
    {
        for (int i = 0; i < Count; i++) _scratch[i] = _world.Create();
        for (int i = 0; i < Count; i++) _world.Destroy(_scratch[i]);
    }

    [Benchmark(OperationsPerInvoke = Count * 2)]
    public void AddRemoveComponent()
    {
        for (int i = 0; i < Count; i++) _world.Add(_entities[i], new Velocity { X = 1, Y = 2 });
        for (int i = 0; i < Count; i++) _world.Remove<Velocity>(_entities[i]);
    }

    [Benchmark(OperationsPerInvoke = Count * 2)]
    public void ReparentAndDetach()
    {
        EntityId parent = _entities[0];
        for (int i = 1; i < Count; i++) _world.SetParent(_entities[i], parent);
        for (int i = 1; i < Count; i++) _world.SetParent(_entities[i], EntityId.None);
    }

    [Benchmark(OperationsPerInvoke = Count * 2)]
    public void CommandBufferCreateAndDestroy()
    {
        CommandBuffer commands = _world.BeginCommands();
        for (int i = 0; i < Count; i++)
        {
            EntityId entity = commands.Create();
            commands.Add(entity, new Position { X = i, Y = i });
            commands.Add(entity, new Velocity { X = 1, Y = 2 });
            _scratch[i] = entity;
        }
        _world.Submit(commands);
        for (int i = 0; i < Count; i++) commands.Destroy(_scratch[i]);
        _world.Submit(commands);
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void BuildChainAndDestroyRecursively()
    {
        EntityId root = _world.Create();
        EntityId parent = root;
        for (int i = 1; i < Count; i++)
        {
            EntityId child = _world.Create();
            _world.SetParent(child, parent);
            parent = child;
        }
        _world.Destroy(root, DestroyMode.Recursive);
    }

    public void Dispose()
    {
        _world?.Dispose();
        GC.SuppressFinalize(this);
    }
}
