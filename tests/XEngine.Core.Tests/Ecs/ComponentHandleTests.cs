using XEngine.Core.Ecs;

namespace XEngine.Core.Tests.Ecs;

public sealed class ComponentHandleTests
{
    [Fact]
    public void Handle_ResolvesComponentType_AndSupportsHotPathApi()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 7 });

        ComponentHandle<Health> handle = world.Handle<Health>();

        Assert.True(world.Has(entity, handle));
        Assert.True(world.TryGet(entity, handle, out Health health));
        Assert.Equal(7, health.Value);

        world.Get(entity, handle).Value = 42;
        Assert.Equal(42, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void Handle_RemainsValid_AfterStructuralChanges()
    {
        using var world = new World();
        EntityId entity = world.Create();
        ComponentHandle<Health> handle = world.Handle<Health>();

        world.Add(entity, new Health { Value = 3 });
        Assert.True(world.Has(entity, handle));
        world.Add(entity, new Velocity());
        Assert.Equal(3, world.Get(entity, handle).Value);
    }

    [Fact]
    public void DefaultHandle_Throws()
    {
        using var world = new World();
        EntityId entity = world.Create();
        ComponentHandle<Health> handle = default;

        Assert.Throws<InvalidOperationException>(() => world.Has(entity, handle));
        Assert.Throws<InvalidOperationException>(() => world.Get(entity, handle));
        Assert.Throws<InvalidOperationException>(() => world.TryGet(entity, handle, out _));
    }

    [Fact]
    public void HandleFromAnotherRegistry_Throws()
    {
        using var firstWorld = new World();
        using var secondWorld = new World();
        EntityId entity = secondWorld.Create();
        secondWorld.Add(entity, new Health { Value = 1 });
        ComponentHandle<Health> foreign = firstWorld.Handle<Health>();

        Assert.Throws<InvalidOperationException>(() => secondWorld.Has(entity, foreign));
        Assert.Throws<InvalidOperationException>(() => secondWorld.Get(entity, foreign));
        Assert.Throws<InvalidOperationException>(() => secondWorld.TryGet(entity, foreign, out _));
    }
}
