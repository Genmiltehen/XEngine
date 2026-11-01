using System.Numerics;
using BenchmarkDotNet.Attributes;
using XEngine.Core.Ecs;
using XEngine.Core.Transforms;

namespace XEngine.Core.Benchmarks;

[MemoryDiagnoser]
public class TransformBenchmarks : IDisposable
{
    private World _world = null!;

    [Params(1_000, 10_000)]
    public int EntityCount { get; set; }

    [Params(HierarchyShape.Flat, HierarchyShape.Chain, HierarchyShape.BinaryTree)]
    public HierarchyShape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        EntityId[] entities = new EntityId[EntityCount];
        for (int i = 0; i < EntityCount; i++)
        {
            EntityId entity = _world.Create();
            _world.Add(entity, new Transform2D
            {
                Position = new Vector2(i % 100, i / 100),
                Rotation = i * 0.01f,
                Scale = new Vector2(1f + (i % 3) * 0.1f),
            });
            entities[i] = entity;
            if (Shape == HierarchyShape.Chain && i > 0) _world.SetParent(entity, entities[i - 1]);
            else if (Shape == HierarchyShape.BinaryTree && i > 0) _world.SetParent(entity, entities[(i - 1) / 2]);
        }
        _world.UpdateTransforms();
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark]
    public void UpdateTransforms() => _world.UpdateTransforms();

    public void Dispose()
    {
        _world?.Dispose();
        GC.SuppressFinalize(this);
    }
}
