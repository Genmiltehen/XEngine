using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;

namespace XEngine.Core.Tests.Ecs;

public sealed class WorldTests
{
    [Fact]
    public void Create_AssignsUniqueIds_AndInitializesNode()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        Assert.NotEqual(first, second);
        Assert.True(world.IsAlive(first));
        Assert.True(world.IsAlive(second));
        Assert.Equal(2, world.EntityCount);
        Node node = world.Get<Node>(first);
        Assert.True(node.ActiveSelf);
        Assert.True(node.Parent.IsNone);
        Assert.True(node.FirstChild.IsNone);
        Assert.Equal(0, node.Depth);
    }

    [Fact]
    public void DefaultEntityId_IsNotAlive()
    {
        using var world = new World();
        world.Create();
        Assert.False(world.IsAlive(default));
    }

    [Fact]
    public void Destroy_InvalidatesHandle_AndRecyclesIndexWithNewVersion()
    {
        using var world = new World();
        EntityId first = world.Create();
        world.Destroy(first);
        Assert.False(world.IsAlive(first));
        Assert.Equal(0, world.EntityCount);
        EntityId recycled = world.Create();
        Assert.Equal(first.Index, recycled.Index);
        Assert.NotEqual(first.Version, recycled.Version);
        Assert.False(world.IsAlive(first));
        Assert.True(world.IsAlive(recycled));
    }

    [Fact]
    public void Add_StoresComponentAndCreatesTransition()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Velocity { Value = new Vector2(3, 4) });
        Assert.True(world.Has<Velocity>(entity));
        Assert.Equal(new Vector2(3, 4), world.Get<Velocity>(entity).Value);
    }

    [Fact]
    public void Add_ExistingComponent_OverwritesValue()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 10 });
        world.Add(entity, new Health { Value = 20 });
        Assert.Equal(20, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void Transition_PreservesComponentsThatStay()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 7 });
        world.Add(entity, new Velocity { Value = new Vector2(1, 2) });
        world.Remove<Health>(entity);
        Assert.False(world.Has<Health>(entity));
        Assert.Equal(new Vector2(1, 2), world.Get<Velocity>(entity).Value);
        Assert.True(world.Has<Node>(entity));
    }

    [Fact]
    public void Remove_MissingComponent_IsNoOp()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Remove<Health>(entity);
        world.Remove<Health>(entity);
        Assert.False(world.Has<Health>(entity));
        Assert.True(world.IsAlive(entity));
    }

    [Fact]
    public void Remove_MovesLastEntityAndKeepsItsData()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId third = world.Create();
        world.Add(first, new Velocity { Value = new Vector2(1, 0) });
        world.Add(second, new Velocity { Value = new Vector2(2, 0) });
        world.Add(third, new Velocity { Value = new Vector2(3, 0) });
        world.Remove<Velocity>(first);
        Assert.False(world.Has<Velocity>(first));
        Assert.Equal(new Vector2(2, 0), world.Get<Velocity>(second).Value);
        Assert.Equal(new Vector2(3, 0), world.Get<Velocity>(third).Value);
        Assert.True(world.Has<Node>(first));
        Assert.True(world.Has<Node>(second));
        Assert.True(world.Has<Node>(third));
    }

    [Fact]
    public void Destroy_MovesLastEntityAndKeepsItsData()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId third = world.Create();
        world.Add(first, new Velocity { Value = new Vector2(1, 0) });
        world.Add(second, new Velocity { Value = new Vector2(2, 0) });
        world.Add(third, new Velocity { Value = new Vector2(3, 0) });
        world.Destroy(first);
        Assert.False(world.IsAlive(first));
        Assert.Equal(2, world.EntityCount);
        Assert.True(world.IsAlive(second));
        Assert.True(world.IsAlive(third));
        Assert.Equal(new Vector2(2, 0), world.Get<Velocity>(second).Value);
        Assert.Equal(new Vector2(3, 0), world.Get<Velocity>(third).Value);
    }

    [Fact]
    public void Get_ReturnsMutableReference()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 1 });
        world.Get<Health>(entity).Value = 42;
        Assert.Equal(42, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void TryGet_ReturnsFalseForMissingComponentAndDeadEntity()
    {
        using var world = new World();
        EntityId entity = world.Create();
        Assert.False(world.TryGet<Health>(entity, out _));
        world.Add(entity, new Health { Value = 5 });
        Assert.True(world.TryGet<Health>(entity, out Health health));
        Assert.Equal(5, health.Value);
        world.Destroy(entity);
        Assert.False(world.TryGet<Health>(entity, out _));
        Assert.False(world.Has<Health>(entity));
    }

    [Fact]
    public void DeadEntity_ThrowsOnAddGetRemove()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Destroy(entity);
        Assert.Throws<InvalidOperationException>(() => world.Add(entity, new Health()));
        Assert.Throws<InvalidOperationException>(() => world.Get<Health>(entity));
        Assert.Throws<InvalidOperationException>(() => world.Remove<Health>(entity));
    }

    [Fact]
    public void Node_CannotBeAddedOrRemovedManually()
    {
        using var world = new World();
        EntityId entity = world.Create();
        Assert.Throws<InvalidOperationException>(() => world.Add(entity, default(Node)));
        Assert.Throws<InvalidOperationException>(() => world.Remove<Node>(entity));
    }

    [Fact]
    public void TagComponents_ShareArchetypeShape()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        world.Add(first, new TagA());
        world.Add(second, new TagA());
        Assert.True(world.Has<TagA>(first));
        Assert.True(world.Has<TagA>(second));
        Assert.False(world.Has<TagB>(first));
        Assert.False(world.Has<TagC>(first));
    }

    [Fact]
    public void RepeatedTransitions_KeepFinalState()
    {
        using var world = new World();
        EntityId entity = world.Create();
        for (int i = 0; i < 100; i++)
        {
            world.Add(entity, new Health { Value = i });
            world.Add(entity, new Velocity { Value = new Vector2(i, -i) });
            world.Remove<Health>(entity);
            world.Add(entity, new TagA());
            world.Remove<TagA>(entity);
        }
        Assert.True(world.Has<Velocity>(entity));
        Assert.Equal(new Vector2(99, -99), world.Get<Velocity>(entity).Value);
        Assert.False(world.Has<Health>(entity));
        Assert.False(world.Has<TagA>(entity));
        Assert.True(world.Has<Node>(entity));
    }

    [Fact]
    public void ComponentSets_UseSharedArchetypes()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        int initialArchetypes = world.ArchetypeCount;
        world.Add(first, new TagA());
        int afterFirst = world.ArchetypeCount;
        world.Add(second, new TagA());
        Assert.Equal(initialArchetypes + 1, afterFirst);
        Assert.Equal(afterFirst, world.ArchetypeCount);
    }
}
