using BenchmarkDotNet.Attributes;
using XEngine.Core.Ecs;

namespace XEngine.Core.Benchmarks;

[MemoryDiagnoser]
public class QueryCallBenchmarks : IDisposable
{
    private const int Calls = 1_000;

    private static readonly QueryAction<Velocity> ReadVelocity = static (ref Velocity velocity) => _ = velocity;

    private static readonly QueryAction<Health> ReadHealth = static (ref Health health) => health.Value++;

    private World _world = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        EntityId entity = _world.Create();
        _world.Add(entity, new Health { Value = 1 });
    }

    [Benchmark(OperationsPerInvoke = Calls)]
    public void EmptyQuery()
    {
        for (int i = 0; i < Calls; i++) _world.Query<Velocity>().ForEach(ReadVelocity);
    }

    [Benchmark(OperationsPerInvoke = Calls)]
    public void SingleEntityQuery()
    {
        for (int i = 0; i < Calls; i++) _world.Query<Health>().ForEach(ReadHealth);
    }

    public void Dispose()
    {
        _world?.Dispose();
        GC.SuppressFinalize(this);
    }
}
