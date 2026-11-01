using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Core.Transforms;

namespace XEngine.Core.Tests.Transforms;

public sealed class TransformSystemTests
{
    [Fact]
    public void RootTransform_ProducesLocalWorldMatrix()
    {
        using var world = new World();
        var system = new TransformSystem();
        EntityId entity = world.Create();
        world.Add(entity, new Transform2D
        {
            Position = new Vector2(10, 20),
            Rotation = 0.5f,
            Scale = new Vector2(2, 3),
        });
        system.Update(world, 0);
        Transform2D transform = world.Get<Transform2D>(entity);
        AssertMatrixNear(transform.LocalMatrix, transform.World);
        AssertNear(new Vector2(10, 20), Vector2.Transform(Vector2.Zero, transform.World));
    }

    [Fact]
    public void ChildWorld_IsLocalTimesParentWorld()
    {
        using var world = new World();
        var system = new TransformSystem();
        EntityId parent = world.Create();
        EntityId child = world.Create();
        world.Add(parent, new Transform2D
        {
            Position = new Vector2(10, 20),
            Rotation = 0.3f,
            Scale = new Vector2(2, 2),
        });
        world.Add(child, new Transform2D
        {
            Position = new Vector2(1, 2),
            Rotation = 0.2f,
            Scale = new Vector2(1.5f, 1.5f),
        });
        world.SetParent(child, parent);
        system.Update(world, 0);
        Matrix3x2 expected =
            world.Get<Transform2D>(child).LocalMatrix *
            world.Get<Transform2D>(parent).LocalMatrix;
        AssertMatrixNear(expected, world.Get<Transform2D>(child).World);
    }

    [Fact]
    public void ParentWithoutTransform_IsIdentity()
    {
        using var world = new World();
        var system = new TransformSystem();
        EntityId parent = world.Create();
        EntityId child = world.Create();
        world.Add(child, Transform2D.At(new Vector2(4, 5)));
        world.SetParent(child, parent);
        system.Update(world, 0);
        AssertNear(new Vector2(4, 5), Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(child).World));
    }

    [Fact]
    public void ParentWithoutTransform_DoesNotBreakGrandparent()
    {
        using var world = new World();
        var system = new TransformSystem();
        EntityId root = world.Create();
        EntityId middle = world.Create();
        EntityId leaf = world.Create();
        world.Add(root, Transform2D.At(new Vector2(10, 0)));
        world.Add(leaf, Transform2D.At(new Vector2(3, 0)));
        world.SetParent(middle, root);
        world.SetParent(leaf, middle);
        system.Update(world, 0);
        AssertNear(new Vector2(13, 0), Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(leaf).World));
    }

    [Fact]
    public void Reparent_UpdatesWorldMatrixOnNextUpdate()
    {
        using var world = new World();
        var system = new TransformSystem();
        EntityId firstParent = world.Create();
        EntityId secondParent = world.Create();
        EntityId child = world.Create();
        world.Add(firstParent, Transform2D.At(new Vector2(10, 0)));
        world.Add(secondParent, Transform2D.At(new Vector2(100, 0)));
        world.Add(child, Transform2D.At(new Vector2(5, 0)));
        world.SetParent(child, firstParent);
        system.Update(world, 0);
        AssertNear(new Vector2(15, 0), Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(child).World));
        world.SetParent(child, secondParent);
        system.Update(world, 0);
        AssertNear(new Vector2(105, 0), Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(child).World));
    }

    [Fact]
    public void DeepHierarchy_UpdatesEveryLevel()
    {
        using var world = new World();
        var system = new TransformSystem();
        EntityId root = world.Create();
        world.Add(root, Transform2D.At(new Vector2(1, 0)));
        EntityId previous = root;
        for (int i = 0; i < 10; i++)
        {
            EntityId child = world.Create();
            world.Add(child, Transform2D.At(new Vector2(1, 0)));
            world.SetParent(child, previous);
            previous = child;
        }
        system.Update(world, 0);
        AssertNear(new Vector2(11, 0), Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(previous).World));
        Assert.Equal(10, world.Get<Node>(previous).Depth);
    }

    [Fact]
    public void MultipleRoots_AreAllUpdated()
    {
        using var world = new World();
        var system = new TransformSystem();
        EntityId first = world.Create();
        EntityId second = world.Create();
        world.Add(first, Transform2D.At(new Vector2(3, 4)));
        world.Add(second, Transform2D.At(new Vector2(-1, 2)));
        system.Update(world, 0);
        AssertNear(new Vector2(3, 4), Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(first).World));
        AssertNear(new Vector2(-1, 2), Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(second).World));
    }

    [Fact]
    public void TransformOnEntityWithoutNode_IsNotPossible()
    {
        using var world = new World();
        EntityId entity = world.Create();
        Assert.True(world.Has<Node>(entity));
        Assert.False(world.Has<Transform2D>(entity));
        Assert.Throws<InvalidOperationException>(() => world.Get<Transform2D>(entity));
    }

    private static void AssertNear(Vector2 expected, Vector2 actual, float epsilon = 1e-4f)
    {
        Assert.InRange(actual.X, expected.X - epsilon, expected.X + epsilon);
        Assert.InRange(actual.Y, expected.Y - epsilon, expected.Y + epsilon);
    }

    private static void AssertMatrixNear(Matrix3x2 expected, Matrix3x2 actual, float epsilon = 1e-4f)
    {
        Assert.InRange(actual.M11, expected.M11 - epsilon, expected.M11 + epsilon);
        Assert.InRange(actual.M12, expected.M12 - epsilon, expected.M12 + epsilon);
        Assert.InRange(actual.M21, expected.M21 - epsilon, expected.M21 + epsilon);
        Assert.InRange(actual.M22, expected.M22 - epsilon, expected.M22 + epsilon);
        Assert.InRange(actual.M31, expected.M31 - epsilon, expected.M31 + epsilon);
        Assert.InRange(actual.M32, expected.M32 - epsilon, expected.M32 + epsilon);
    }
}
