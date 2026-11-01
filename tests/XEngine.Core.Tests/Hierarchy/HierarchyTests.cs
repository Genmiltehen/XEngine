using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Core.Transforms;

namespace XEngine.Core.Tests.Hierarchy;

public sealed class HierarchyTests
{
    [Fact]
    public void SetParent_LinksChildrenInStableOrder()
    {
        using var world = new World();
        EntityId parent = world.Create();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId third = world.Create();
        world.SetParent(first, parent);
        world.SetParent(second, parent);
        world.SetParent(third, parent);
        Node parentNode = world.Get<Node>(parent);
        Assert.Equal(first, parentNode.FirstChild);
        Assert.Equal(third, parentNode.LastChild);
        Assert.Equal(second, world.Get<Node>(first).NextSibling);
        Assert.Equal(second, world.Get<Node>(third).PrevSibling);
        Assert.True(world.Get<Node>(first).PrevSibling.IsNone);
        Assert.True(world.Get<Node>(third).NextSibling.IsNone);
        Assert.Equal(1, world.Get<Node>(first).Depth);
        Assert.Equal(1, world.Get<Node>(third).Depth);
    }

    [Fact]
    public void SetParent_MovesBetweenParents()
    {
        using var world = new World();
        EntityId firstParent = world.Create();
        EntityId secondParent = world.Create();
        EntityId child = world.Create();
        world.SetParent(child, firstParent);
        world.SetParent(child, secondParent);
        Assert.True(world.Get<Node>(firstParent).FirstChild.IsNone);
        Assert.True(world.Get<Node>(firstParent).LastChild.IsNone);
        Assert.Equal(child, world.Get<Node>(secondParent).FirstChild);
        Assert.Equal(secondParent, world.Get<Node>(child).Parent);
    }

    [Fact]
    public void SetParent_DepthUpdatesForWholeSubtree()
    {
        using var world = new World();
        EntityId root = world.Create();
        EntityId middle = world.Create();
        EntityId leaf = world.Create();
        world.SetParent(middle, root);
        world.SetParent(leaf, middle);
        Assert.Equal(2, world.Get<Node>(leaf).Depth);
        world.SetParent(middle, EntityId.None);
        Assert.Equal(0, world.Get<Node>(middle).Depth);
        Assert.Equal(1, world.Get<Node>(leaf).Depth);
    }

    [Fact]
    public void SetParent_Self_Throws()
    {
        using var world = new World();
        EntityId entity = world.Create();
        Assert.Throws<InvalidOperationException>(() => world.SetParent(entity, entity));
    }

    [Fact]
    public void SetParent_Cycle_Throws()
    {
        using var world = new World();
        EntityId root = world.Create();
        EntityId middle = world.Create();
        EntityId leaf = world.Create();
        world.SetParent(middle, root);
        world.SetParent(leaf, middle);
        Assert.Throws<InvalidOperationException>(() => world.SetParent(root, leaf));
        Assert.Throws<InvalidOperationException>(() => world.SetParent(root, middle));
    }

    [Fact]
    public void SetParent_DeadEntity_Throws()
    {
        using var world = new World();
        EntityId parent = world.Create();
        EntityId child = world.Create();
        world.Destroy(parent);
        Assert.Throws<InvalidOperationException>(() => world.SetParent(child, parent));
        Assert.Throws<InvalidOperationException>(() => world.SetParent(parent, child));
    }

    [Fact]
    public void SetActive_PropagatesActiveInHierarchy()
    {
        using var world = new World();
        EntityId root = world.Create();
        EntityId child = world.Create();
        EntityId grandchild = world.Create();
        world.SetParent(child, root);
        world.SetParent(grandchild, child);
        Assert.True(world.Get<Node>(grandchild).ActiveInHierarchy);
        world.SetActive(root, false);
        Assert.False(world.Get<Node>(root).ActiveInHierarchy);
        Assert.False(world.Get<Node>(child).ActiveInHierarchy);
        Assert.False(world.Get<Node>(grandchild).ActiveInHierarchy);
        Assert.True(world.Get<Node>(child).ActiveSelf);
        world.SetActive(root, true);
        Assert.True(world.Get<Node>(grandchild).ActiveInHierarchy);
        world.SetActive(child, false);
        Assert.False(world.Get<Node>(child).ActiveInHierarchy);
        Assert.False(world.Get<Node>(grandchild).ActiveInHierarchy);
        world.SetActive(root, false);
        world.SetActive(root, true);
        Assert.False(world.Get<Node>(grandchild).ActiveInHierarchy);
        world.SetActive(child, true);
        Assert.True(world.Get<Node>(grandchild).ActiveInHierarchy);
    }

    [Fact]
    public void SetParent_UnderInactiveParent_DisablesHierarchy()
    {
        using var world = new World();
        EntityId inactiveParent = world.Create();
        EntityId child = world.Create();
        world.SetActive(inactiveParent, false);
        world.SetParent(child, inactiveParent);
        Assert.False(world.Get<Node>(child).ActiveInHierarchy);
    }

    [Fact]
    public void Destroy_DefaultReparentsChildrenToParent()
    {
        using var world = new World();
        EntityId grandparent = world.Create();
        EntityId parent = world.Create();
        EntityId first = world.Create();
        EntityId second = world.Create();
        world.SetParent(parent, grandparent);
        world.SetParent(first, parent);
        world.SetParent(second, parent);
        world.Destroy(parent);
        Assert.False(world.IsAlive(parent));
        Assert.Equal(grandparent, world.Get<Node>(first).Parent);
        Assert.Equal(grandparent, world.Get<Node>(second).Parent);
        Assert.Equal(1, world.Get<Node>(first).Depth);
        Assert.Equal(1, world.Get<Node>(second).Depth);
        Assert.Equal(first, world.Get<Node>(grandparent).FirstChild);
        Assert.Equal(second, world.Get<Node>(grandparent).LastChild);
        Assert.Equal(second, world.Get<Node>(first).NextSibling);
    }

    [Fact]
    public void Destroy_DefaultMakesRootChildrenRoots()
    {
        using var world = new World();
        EntityId parent = world.Create();
        EntityId child = world.Create();
        world.SetParent(child, parent);
        world.Destroy(parent);
        Assert.True(world.Get<Node>(child).Parent.IsNone);
        Assert.Equal(0, world.Get<Node>(child).Depth);
        Assert.True(world.Get<Node>(child).ActiveInHierarchy);
    }

    [Fact]
    public void Destroy_DefaultFixesSiblingLinks()
    {
        using var world = new World();
        EntityId parent = world.Create();
        EntityId first = world.Create();
        EntityId middle = world.Create();
        EntityId last = world.Create();
        world.SetParent(first, parent);
        world.SetParent(middle, parent);
        world.SetParent(last, parent);
        world.Destroy(middle);
        Assert.Equal(last, world.Get<Node>(first).NextSibling);
        Assert.Equal(first, world.Get<Node>(last).PrevSibling);
        Assert.Equal(last, world.Get<Node>(parent).LastChild);
        Assert.False(world.IsAlive(middle));
    }

    [Fact]
    public void Destroy_Recursive_DestroysWholeSubtree()
    {
        using var world = new World();
        EntityId grandparent = world.Create();
        EntityId root = world.Create();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId survivor = world.Create();
        world.SetParent(root, grandparent);
        world.SetParent(first, root);
        world.SetParent(second, first);
        world.SetParent(survivor, grandparent);
        world.Destroy(root, DestroyMode.Recursive);
        Assert.False(world.IsAlive(root));
        Assert.False(world.IsAlive(first));
        Assert.False(world.IsAlive(second));
        Assert.True(world.IsAlive(survivor));
        Assert.Equal(survivor, world.Get<Node>(grandparent).FirstChild);
        Assert.True(world.Get<Node>(survivor).PrevSibling.IsNone);
    }

    [Fact]
    public void Destroy_PreserveWorld_KeepsChildInPlace()
    {
        using var world = new World();
        var transforms = new TransformSystem();
        EntityId parent = world.Create();
        world.Add(parent, Transform2D.At(new Vector2(10, 0)));
        EntityId child = world.Create();
        world.Add(child, Transform2D.At(new Vector2(5, 0)));
        world.SetParent(child, parent);
        transforms.Update(world, 0);
        Vector2 before = Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(child).World);
        world.Destroy(parent, keepWorld: true);
        transforms.Update(world, 0);
        Vector2 after = Vector2.Transform(Vector2.Zero, world.Get<Transform2D>(child).World);
        AssertNear(before, after);
        AssertNear(new Vector2(15, 0), after);
    }

    [Fact]
    public void SetParent_PreserveWorld_KeepsWorldTransform()
    {
        using var world = new World();
        var transforms = new TransformSystem();
        EntityId firstParent = world.Create();
        world.Add(firstParent, Transform2D.At(new Vector2(10, 0)));
        EntityId secondParent = world.Create();
        world.Add(secondParent, Transform2D.At(new Vector2(100, 50)));
        EntityId child = world.Create();
        world.Add(child, Transform2D.At(new Vector2(5, 0)));
        world.SetParent(child, firstParent);
        transforms.Update(world, 0);
        Matrix3x2 before = world.Get<Transform2D>(child).World;
        world.SetParent(child, secondParent, keepWorld: true);
        AssertNear(before, world.Get<Transform2D>(child).World);
        transforms.Update(world, 0);
        AssertNear(before, world.Get<Transform2D>(child).World);
        AssertNear(new Vector2(-85, -50), world.Get<Transform2D>(child).Position);
    }

    [Fact]
    public void SetParent_PreserveWorld_WithRotationAndUniformScale()
    {
        using var world = new World();
        var transforms = new TransformSystem();
        EntityId firstParent = world.Create();
        world.Add(firstParent, new Transform2D
        {
            Position = new Vector2(10, 20),
            Rotation = 0.4f,
            Scale = new Vector2(2f, 2f),
        });
        EntityId secondParent = world.Create();
        world.Add(secondParent, new Transform2D
        {
            Position = new Vector2(-30, 5),
            Rotation = -0.9f,
            Scale = new Vector2(1.5f, 1.5f),
        });
        EntityId child = world.Create();
        world.Add(child, new Transform2D
        {
            Position = new Vector2(3, -2),
            Rotation = 0.25f,
            Scale = new Vector2(1.2f, 1.2f),
        });
        world.SetParent(child, firstParent);
        transforms.Update(world, 0);
        Matrix3x2 before = world.Get<Transform2D>(child).World;
        Vector2 beforePoint = Vector2.Transform(new Vector2(1, 1), before);
        world.SetParent(child, secondParent, keepWorld: true);
        transforms.Update(world, 0);
        Matrix3x2 after = world.Get<Transform2D>(child).World;
        Vector2 afterPoint = Vector2.Transform(new Vector2(1, 1), after);
        AssertNear(beforePoint, afterPoint, 1e-3f);
    }

    [Fact]
    public void Children_IteratesInSiblingOrder()
    {
        using var world = new World();
        EntityId parent = world.Create();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId third = world.Create();
        world.SetParent(first, parent);
        world.SetParent(second, parent);
        world.SetParent(third, parent);
        List<EntityId> children = [];
        foreach (EntityId child in world.Children(parent)) children.Add(child);
        Assert.Equal(new[] { first, second, third }, children);
    }

    [Fact]
    public void Children_WithoutChildren_IsEmpty()
    {
        using var world = new World();
        EntityId entity = world.Create();
        List<EntityId> children = [];
        foreach (EntityId child in world.Children(entity)) children.Add(child);
        Assert.Empty(children);
    }

    [Fact]
    public void Children_ReparentingCurrentDuringIteration_VisitsAllChildren()
    {
        using var world = new World();
        EntityId root = world.Create();
        EntityId parent = world.Create();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId third = world.Create();
        world.SetParent(parent, root);
        world.SetParent(first, parent);
        world.SetParent(second, parent);
        world.SetParent(third, parent);
        List<EntityId> visited = [];
        foreach (EntityId child in world.Children(parent))
        {
            visited.Add(child);
            world.SetParent(child, root);
        }
        Assert.Equal(new[] { first, second, third }, visited);
        Assert.True(world.Get<Node>(parent).FirstChild.IsNone);
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
