using BenchmarkDotNet.Attributes;
using XEngine.Core.Ecs;

namespace XEngine.Core.Benchmarks;

[MemoryDiagnoser]
public class QueryBenchmarks : IDisposable
{
    private static readonly QueryAction<Position> ReadPosition =
        static (ref Position position) => _sink += (long)position.X;

    private static readonly QueryAction<Position, Velocity> ReadPositionVelocity =
        static (ref Position position, ref Velocity velocity) =>
            _sink += (long)(position.X + velocity.Y);

    private static readonly QueryAction<Position, Velocity, Health> ReadThree =
        static (ref Position position, ref Velocity velocity, ref Health health) =>
            _sink += (long)(position.X + velocity.Y + health.Value);

    private static long _sink;

    private World _world = null!;

    [Params(10_000, 100_000)]
    public int EntityCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        for (int i = 0; i < EntityCount; i++)
        {
            EntityId entity = _world.Create();
            _world.Add(entity, new Position { X = i, Y = -i });
            switch (i % 4)
            {
                case 1:
                    _world.Add(entity, new Velocity { X = 1, Y = 2 });
                    break;
                case 2:
                    _world.Add(entity, new Velocity { X = 1, Y = 2 });
                    _world.Add(entity, new Health { Value = 3 });
                    break;
                case 3:
                    _world.Add(entity, new Health { Value = 3 });
                    break;
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark]
    public void OneComponent() => _world.Query<Position>().ForEach(ReadPosition);

    [Benchmark]
    public void TwoComponents() =>
        _world.Query<Position, Velocity>().ForEach(ReadPositionVelocity);

    [Benchmark]
    public void ThreeComponents() => _world.Query<Position, Velocity, Health>().ForEach(ReadThree);

    [Benchmark]
    public void Filtered() =>
        _world.Query<Position>().Without<Health>().ForEach(ReadPosition);

    [Benchmark]
    public int Count()
    {
        int count = _world.Query<Position>().Count();
        _sink += count;
        return count;
    }

    public void Dispose()
    {
        _world?.Dispose();
        GC.SuppressFinalize(this);
    }
}
