using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Systems;
using XEngine.Common.Transform;

namespace XEngine.Core.Tests.Systems;

public sealed class WorldLoopTests
{
    [Fact]
    public void Update_RunsSystemsInPhaseThenRegistrationOrder()
    {
        List<string> calls = [];
        using var world = new World(world =>
        {
            world.AddSystem(SystemPhase.PostUpdate, new RecordingSystem("post-1", calls));
            world.AddSystem(SystemPhase.Update, new RecordingSystem("update-1", calls));
            world.AddSystem(SystemPhase.PreUpdate, new RecordingSystem("pre-1", calls));
            world.AddSystem(SystemPhase.Update, new RecordingSystem("update-2", calls));
            world.AddSystem(SystemPhase.Transform, new RecordingSystem("transform-1", calls));
            world.AddSystem(SystemPhase.PostUpdate, new RecordingSystem("post-2", calls));
        });
        world.Update(0.5f);
        Assert.Equal(
            ["pre-1", "update-1", "update-2", "post-1", "post-2", "transform-1"],
            calls);
    }

    [Fact]
    public void Update_TracksGameTimeAndPassesDeltaToSystems()
    {
        List<float> deltas = [];
        using var world = new World(world =>
            world.AddSystem(SystemPhase.Update, new DeltaRecordingSystem(deltas)));
        Assert.Equal(0, world.Time.FrameCount);
        Assert.Equal(0f, world.Time.TotalTime);
        world.Update(0.25f);
        world.Update(0.5f);
        Assert.Equal([0.25f, 0.5f], deltas);
        Assert.Equal(0.75f, world.Time.TotalTime);
        Assert.Equal(2, world.Time.FrameCount);
        Assert.Equal(0.5f, world.Time.DeltaTime);
    }

    [Fact]
    public void ConfiguredWorld_ComputesTransformsOnUpdate()
    {
        using var world = new World(TransformWorldConfigurator.Default);
        EntityId entity = world.Create();
        world.Add(entity, Transform2D.At(new Vector2(5, 7)));
        world.Update(0f);
        AssertNear(
            new Vector2(5, 7),
            Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(entity).World));
    }

    [Fact]
    public void UpdateTransforms_RunsOnlyTransformPhase()
    {
        int gameplayCalls = 0;
        using var world = new World(world =>
        {
            world.AddSystem(SystemPhase.Transform, new TransformSystem());
            world.AddSystem(SystemPhase.Update, new CountingSystem(() => gameplayCalls++));
        });
        EntityId parent = world.Create();
        world.Add(parent, Transform2D.At(new Vector2(10, 0)));
        EntityId child = world.Create();
        world.Add(child, Transform2D.At(new Vector2(2, 3)));
        world.SetParent(child, parent);
        world.UpdateTransforms();
        Assert.Equal(0, gameplayCalls);
        Assert.Equal(0, world.Time.FrameCount);
        AssertNear(
            new Vector2(12, 3),
            Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(child).World));
    }

    [Fact]
    public void Resources_CanBeSetReadReplacedAndRemoved()
    {
        using var world = new World();
        Assert.Throws<InvalidOperationException>(() => world.GetResource<TestResource>());
        Assert.False(world.TryGetResource<TestResource>(out _));
        var first = new TestResource { Name = "first" };
        world.SetResource(first);
        Assert.Same(first, world.GetResource<TestResource>());
        Assert.True(world.TryGetResource<TestResource>(out TestResource? stored));
        Assert.Same(first, stored);
        var second = new TestResource { Name = "second" };
        world.SetResource(second);
        Assert.Same(second, world.GetResource<TestResource>());
        Assert.True(first.Disposed);
        Assert.True(world.RemoveResource<TestResource>());
        Assert.False(world.RemoveResource<TestResource>());
        Assert.True(second.Disposed);
        Assert.False(world.TryGetResource<TestResource>(out _));
    }

    [Fact]
    public void Resources_OfDifferentTypes_AreIndependent()
    {
        using var world = new World();
        var first = new TestResource();
        var second = new OtherResource();
        world.SetResource(first);
        world.SetResource(second);
        Assert.Same(first, world.GetResource<TestResource>());
        Assert.Same(second, world.GetResource<OtherResource>());
    }

    [Fact]
    public void Dispose_DisposesSystemsAndResources_AndIsIdempotent()
    {
        var system = new DisposableSystem();
        var resource = new OtherResource();
        var world = new World(w => w.AddSystem(SystemPhase.Update, system));
        world.SetResource(resource);
        world.Dispose();
        world.Dispose();
        Assert.Equal(1, system.DisposeCount);
        Assert.Equal(1, resource.DisposeCount);
    }

    [Fact]
    public void DisposedWorld_ThrowsOnUpdateAndMutation()
    {
        var world = new World();
        world.Dispose();
        Assert.Throws<ObjectDisposedException>(() => world.Update(0f));
        Assert.Throws<ObjectDisposedException>(() => world.UpdateTransforms());
        Assert.Throws<ObjectDisposedException>(() => world.AddSystem(SystemPhase.Update, new CountingSystem(() => { })));
        Assert.Throws<ObjectDisposedException>(() => world.SetResource(new TestResource()));
        Assert.Throws<ObjectDisposedException>(() => world.GetResource<TestResource>());
    }

    [Fact]
    public void RecursiveUpdate_Throws()
    {
        World? world = null;
        world = new World(w => w.AddSystem(SystemPhase.Update, new CountingSystem(() => world!.Update(0f))));
        Assert.Throws<InvalidOperationException>(() => world.Update(0f));
    }

    [Fact]
    public void SystemAddedDuringUpdate_StartsNextFrame()
    {
        List<string> calls = [];
        using var world = new World();
        var late = new RecordingSystem("late", calls);
        bool added = false;
        world.AddSystem(SystemPhase.Update, new CountingSystem(() =>
        {
            calls.Add("first");
            if (!added)
            {
                added = true;
                world.AddSystem(SystemPhase.Update, late);
            }
        }));
        world.Update(0f);
        Assert.Equal(["first"], calls);
        world.Update(0f);
        Assert.Equal(["first", "first", "late"], calls);
    }

    [Fact]
    public void AddSystem_NullOrUnknownPhase_Throws()
    {
        using var world = new World();
        Assert.Throws<ArgumentNullException>(() => world.AddSystem(SystemPhase.Update, null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => world.AddSystem((SystemPhase)99, new CountingSystem(() => { })));
    }

    private static void AssertNear(Vector2 expected, Vector2 actual, float epsilon = 1e-4f)
    {
        Assert.InRange(actual.X, expected.X - epsilon, expected.X + epsilon);
        Assert.InRange(actual.Y, expected.Y - epsilon, expected.Y + epsilon);
    }

    private sealed class RecordingSystem(string name, List<string> calls) : ISystem
    {
        public void Update(World world, float dt) => calls.Add(name);
    }

    private sealed class DeltaRecordingSystem(List<float> deltas) : ISystem
    {
        public void Update(World world, float dt) => deltas.Add(dt);
    }

    private sealed class CountingSystem(Action action) : ISystem
    {
        public void Update(World world, float dt) => action();
    }

    private sealed class DisposableSystem : ISystem, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Update(World world, float dt) { }

        public void Dispose() => DisposeCount++;
    }

    private sealed class TestResource : IDisposable
    {
        public string Name { get; set; } = "";

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class OtherResource : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
