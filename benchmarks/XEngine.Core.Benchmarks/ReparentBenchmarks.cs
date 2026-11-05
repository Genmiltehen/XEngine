using BenchmarkDotNet.Attributes;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;

namespace XEngine.Core.Benchmarks;

[MemoryDiagnoser]
public class ReparentBenchmarks : IDisposable
{
    private const int Count = 1_000;

    private World _world = null!;
    private EntityId[] _entities = null!;

    [Params(false, true)]
    public bool WithBeforeReparentListener { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        _entities = new EntityId[Count];
        for (int i = 0; i < Count; i++) _entities[i] = _world.Create();
        if (WithBeforeReparentListener) _world.BeforeReparent += static (World world, in BeforeReparentEventArgs args) => { };
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(OperationsPerInvoke = (Count - 1) * 2)]
    public void ReparentAndDetach()
    {
        EntityId parent = _entities[0];
        for (int i = 1; i < Count; i++) _world.SetParent(_entities[i], parent);
        for (int i = 1; i < Count; i++) _world.SetParent(_entities[i], EntityId.None);
    }

    public void Dispose()
    {
        _world?.Dispose();
        GC.SuppressFinalize(this);
    }
}
