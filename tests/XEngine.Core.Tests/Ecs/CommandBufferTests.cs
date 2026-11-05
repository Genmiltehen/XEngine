using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Common.Transform;

namespace XEngine.Core.Tests.Ecs;

public sealed class CommandBufferTests
{
    [Fact]
    public void CreateAndAdd_AreAppliedOnSubmit()
    {
        using var world = new World();
        CommandBuffer commands = world.BeginCommands();
        EntityId entity = commands.Create();
        commands.Add(entity, new Health { Value = 5 });
        commands.Add(entity, new Velocity { Value = new Vector2(1, 2) });
        Assert.False(world.IsAlive(entity));
        Assert.Equal(0, world.EntityCount);
        world.Submit(commands);
        Assert.True(world.IsAlive(entity));
        Assert.Equal(1, world.EntityCount);
        Assert.Equal(5, world.Get<Health>(entity).Value);
        Assert.Equal(new Vector2(1, 2), world.Get<Velocity>(entity).Value);
        Assert.True(world.Has<Node>(entity));
    }

    [Fact]
    public void CreateThenDestroyInSameBuffer_CancelsBoth()
    {
        using var world = new World();
        CommandBuffer commands = world.BeginCommands();
        EntityId entity = commands.Create();
        commands.Add(entity, new Health());
        commands.Destroy(entity);
        world.Submit(commands);
        Assert.False(world.IsAlive(entity));
        Assert.Equal(0, world.EntityCount);
    }

    [Fact]
    public void AddThenRemove_LastOperationWins()
    {
        using var world = new World();
        EntityId entity = world.Create();
        CommandBuffer commands = world.BeginCommands();
        commands.Add(entity, new Health { Value = 1 });
        commands.Remove<Health>(entity);
        world.Submit(commands);
        Assert.False(world.Has<Health>(entity));
    }

    [Fact]
    public void RemoveThenAdd_LastOperationWins()
    {
        using var world = new World();
        EntityId entity = world.Create();
        CommandBuffer commands = world.BeginCommands();
        commands.Remove<Health>(entity);
        commands.Add(entity, new Health { Value = 2 });
        world.Submit(commands);
        Assert.True(world.Has<Health>(entity));
        Assert.Equal(2, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void DuplicateAdd_UsesLastValue()
    {
        using var world = new World();
        EntityId entity = world.Create();
        CommandBuffer commands = world.BeginCommands();
        commands.Add(entity, new Health { Value = 1 });
        commands.Add(entity, new Health { Value = 9 });
        world.Submit(commands);
        Assert.Equal(9, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void AddRemoveComponents_OnExistingEntity_AreAggregated()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new TagA());
        world.Add(entity, new Health { Value = 1 });
        int archetypesBefore = world.ArchetypeCount;
        CommandBuffer commands = world.BeginCommands();
        commands.Remove<TagA>(entity);
        commands.Add(entity, new TagB());
        commands.Add(entity, new TagC());
        commands.Add(entity, new Velocity { Value = new Vector2(4, 5) });
        world.Submit(commands);
        Assert.False(world.Has<TagA>(entity));
        Assert.True(world.Has<TagB>(entity));
        Assert.True(world.Has<TagC>(entity));
        Assert.Equal(new Vector2(4, 5), world.Get<Velocity>(entity).Value);
        Assert.Equal(1, world.Get<Health>(entity).Value);
        Assert.True(world.ArchetypeCount > archetypesBefore);
    }

    [Fact]
    public void DestroyInBuffer_RemovesExistingEntity()
    {
        using var world = new World();
        EntityId entity = world.Create();
        CommandBuffer commands = world.BeginCommands();
        commands.Destroy(entity);
        world.Submit(commands);
        Assert.False(world.IsAlive(entity));
        Assert.Equal(0, world.EntityCount);
    }

    [Fact]
    public void BufferEntity_CanBeReferencedByLaterCommands()
    {
        using var world = new World();
        CommandBuffer commands = world.BeginCommands();
        EntityId entity = commands.Create();
        commands.Add(entity, new Health { Value = 3 });
        commands.Remove<Health>(entity);
        commands.Add(entity, new Health { Value = 4 });
        world.Submit(commands);
        Assert.Equal(4, world.Get<Health>(entity).Value);
    }

#if DEBUG
    [Fact]
    public void SubmitInsideForEach_Throws()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health());
        Assert.Throws<InvalidOperationException>(() =>
            world.Query<Health>().ForEach((ref Health health) =>
            {
                _ = health;
                CommandBuffer commands = world.BeginCommands();
                EntityId created = commands.Create();
                commands.Add(created, new Velocity());
                world.Submit(commands);
            }));
    }
#endif

    [Fact]
    public void BufferCanBeReusedAfterSubmit()
    {
        using var world = new World();
        CommandBuffer commands = world.BeginCommands();
        EntityId first = commands.Create();
        world.Submit(commands);
        EntityId second = commands.Create();
        commands.Add(second, new Health { Value = 1 });
        world.Submit(commands);
        Assert.True(world.IsAlive(first));
        Assert.True(world.IsAlive(second));
        Assert.Equal(2, world.EntityCount);
        Assert.Equal(1, world.Get<Health>(second).Value);
    }

    [Fact]
    public void BufferedHierarchyChanges_AreAppliedAfterCreation()
    {
        using var world = new World();
        EntityId parent = world.Create();
        CommandBuffer commands = world.BeginCommands();
        EntityId child = commands.Create();
        commands.Add(child, Transform2D.At(new Vector2(2, 3)));
        commands.SetParent(child, parent);
        world.Submit(commands);
        Assert.Equal(parent, world.Get<Node>(child).Parent);
        Assert.Equal(child, world.Get<Node>(parent).FirstChild);
        Assert.Equal(1, world.Get<Node>(child).Depth);
    }

    [Fact]
    public void BufferCanCreateWholeHierarchyWithChildrenBeforeParents()
    {
        using var world = new World(TransformWorldConfigurator.Default);
        CommandBuffer commands = world.BeginCommands();
        EntityId child = commands.Create();
        EntityId parent = commands.Create();
        commands.Add(parent, Transform2D.At(new Vector2(10, 0)));
        commands.Add(child, Transform2D.At(new Vector2(1, 2)));
        commands.SetParent(child, parent);
        world.Submit(commands);
        world.Update(0f);
        Assert.True(world.IsAlive(child));
        Assert.True(world.IsAlive(parent));
        Assert.Equal(parent, world.Get<Node>(child).Parent);
        Assert.Equal(1, world.Get<Node>(child).Depth);
        AssertNear(
            new Vector2(11, 2),
            Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(child).World));
    }

    [Fact]
    public void BufferedReparent_CanPreserveWorldTransform()
    {
        using var world = new World(TransformWorldConfigurator.Default);
        EntityId firstParent = world.Create();
        world.Add(firstParent, Transform2D.At(new Vector2(10, 0)));
        EntityId secondParent = world.Create();
        world.Add(secondParent, Transform2D.At(new Vector2(100, 0)));
        EntityId child = world.Create();
        world.Add(child, Transform2D.At(new Vector2(5, 0)));
        world.SetParent(child, firstParent);
        world.Update(0f);
        world.Get<Transform2D>(child).Flags = TransformFlags.KeepWorldOnDetach;
        Matrix3x2 before = world.Get<Transform2D>(child).World;
        CommandBuffer commands = world.BeginCommands();
        commands.SetParent(child, secondParent);
        world.Submit(commands);
        AssertNear(before, world.Get<Transform2D>(child).World);
        world.Update(0f);
        AssertNear(before, world.Get<Transform2D>(child).World);
    }

    [Fact]
    public void BufferedSetParent_RaisesBeforeReparent()
    {
        using var world = new World();
        EntityId firstParent = world.Create();
        EntityId secondParent = world.Create();
        EntityId child = world.Create();
        world.SetParent(child, firstParent);
        List<EntityId> previousParents = [];
        world.BeforeReparent += (World _, in BeforeReparentEventArgs args) => previousParents.Add(args.PreviousParent);
        CommandBuffer commands = world.BeginCommands();
        commands.SetParent(child, secondParent);
        world.Submit(commands);
        Assert.Equal([firstParent], previousParents);
        Assert.Equal(secondParent, world.Get<Node>(child).Parent);
    }

    [Fact]
    public void BufferedRecursiveDestroy_RemovesSubtree()
    {
        using var world = new World();
        EntityId root = world.Create();
        EntityId child = world.Create();
        EntityId grandchild = world.Create();
        world.SetParent(child, root);
        world.SetParent(grandchild, child);
        CommandBuffer commands = world.BeginCommands();
        commands.Destroy(root, DestroyMode.Recursive);
        world.Submit(commands);
        Assert.False(world.IsAlive(root));
        Assert.False(world.IsAlive(child));
        Assert.False(world.IsAlive(grandchild));
        Assert.Equal(0, world.EntityCount);
    }

    private static void AssertNear(Vector2 expected, Vector2 actual, float epsilon = 1e-4f)
    {
        Assert.InRange(actual.X, expected.X - epsilon, expected.X + epsilon);
        Assert.InRange(actual.Y, expected.Y - epsilon, expected.Y + epsilon);
    }

    private static void AssertNear(Matrix3x2 expected, Matrix3x2 actual, float epsilon = 1e-4f)
    {
        Assert.InRange(actual.M11, expected.M11 - epsilon, expected.M11 + epsilon);
        Assert.InRange(actual.M12, expected.M12 - epsilon, expected.M12 + epsilon);
        Assert.InRange(actual.M21, expected.M21 - epsilon, expected.M21 + epsilon);
        Assert.InRange(actual.M22, expected.M22 - epsilon, expected.M22 + epsilon);
        Assert.InRange(actual.M31, expected.M31 - epsilon, expected.M31 + epsilon);
        Assert.InRange(actual.M32, expected.M32 - epsilon, expected.M32 + epsilon);
    }
}
