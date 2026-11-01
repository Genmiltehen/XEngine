using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Core.Transforms;

namespace XEngine.Core.Tests.Ecs;

public sealed class WorldFuzzTests
{
    private static readonly Type[] FuzzComponents =
    [
        typeof(FuzzA),
        typeof(FuzzB),
        typeof(FuzzC),
        typeof(Transform2D),
    ];

    [Theory]
    [InlineData(12345)]
    [InlineData(987654321)]
    public void RandomOperations_MatchReferenceModel(int seed)
    {
        var random = new Random(seed);
        using var world = new World();
        var model = new ReferenceModel();
        for (int step = 0; step < 1500; step++)
        {
            int roll = random.Next(100);
            if (roll < 15 || model.Entities.Count == 0)
            {
                if (model.Entities.Count >= 80) DestroyRandom(random, world, model);
                else
                {
                    EntityId entity = world.Create();
                    model.Create(entity);
                    Assert.True(world.Has<Node>(entity));
                }
            }
            else if (roll < 35)
            {
                EntityId entity = model.Pick(random);
                Type type = FuzzComponents[random.Next(FuzzComponents.Length)];
                switch (type)
                {
                    case var t when t == typeof(FuzzA):
                        world.Add(entity, new FuzzA { Value = random.Next(100) });
                        break;
                    case var t when t == typeof(FuzzB):
                        world.Add(entity, new FuzzB { Value = random.Next(100) });
                        break;
                    case var t when t == typeof(FuzzC):
                        world.Add(entity, new FuzzC());
                        break;
                    default:
                        world.Add(entity, RandomTransform(random));
                        break;
                }
                model.Add(entity, type);
            }
            else if (roll < 50)
            {
                EntityId entity = model.Pick(random);
                Type? type = model.PickComponent(random, entity);
                if (type is not null)
                {
                    switch (type)
                    {
                        case var t when t == typeof(FuzzA):
                            world.Remove<FuzzA>(entity);
                            break;
                        case var t when t == typeof(FuzzB):
                            world.Remove<FuzzB>(entity);
                            break;
                        case var t when t == typeof(FuzzC):
                            world.Remove<FuzzC>(entity);
                            break;
                        default:
                            world.Remove<Transform2D>(entity);
                            break;
                    }
                    model.Remove(entity, type);
                }
            }
            else if (roll < 70)
            {
                EntityId child = model.Pick(random);
                EntityId parent = random.Next(5) == 0 ? EntityId.None : model.Pick(random);
                if (model.WouldCreateCycle(child, parent))
                {
                    Assert.Throws<InvalidOperationException>(() => world.SetParent(child, parent));
                }
                else
                {
                    world.SetParent(child, parent);
                    model.SetParent(child, parent);
                }
            }
            else if (roll < 80)
            {
                EntityId entity = model.Pick(random);
                bool active = !model.ActiveSelf[entity];
                world.SetActive(entity, active);
                model.ActiveSelf[entity] = active;
            }
            else if (roll < 92) DestroyRandom(random, world, model);
            else
            {
                EntityId entity = model.Pick(random);
                world.Destroy(entity, DestroyMode.Recursive);
                model.DestroySubtree(entity);
            }
            Validate(world, model);
        }
    }

    private static void DestroyRandom(Random random, World world, ReferenceModel model)
    {
        EntityId entity = model.Pick(random);
        world.Destroy(entity);
        model.DestroyReparentingChildren(entity);
    }

    private static void Validate(World world, ReferenceModel model)
    {
        world.UpdateTransforms();
        List<EntityId> worldEntities = [];
        world.Query<Node>().ForEach((EntityId entity, ref Node node) =>
        {
            _ = node;
            worldEntities.Add(entity);
        });
        Assert.Equal(model.Entities.Count, world.EntityCount);
        Assert.Equal(model.Entities.Count, worldEntities.Count);
        Assert.Equal(model.Entities.Count, worldEntities.ToHashSet().Count);
        Dictionary<EntityId, List<EntityId>> children = [];
        foreach (EntityId entity in worldEntities)
        {
            Assert.Contains(entity, model.Entities);
            Node node = world.Get<Node>(entity);
            Assert.Equal(model.ParentOf(entity), node.Parent);
            Assert.Equal(model.DepthOf(entity), node.Depth);
            Assert.Equal(model.ActiveSelf[entity], node.ActiveSelf);
            Assert.Equal(model.IsActiveInHierarchy(entity), node.ActiveInHierarchy);
            ValidateComponents(world, model, entity);
            ValidateTransform(world, model, entity);
            if (!children.TryGetValue(node.Parent, out List<EntityId>? list))
            {
                list = [];
                children.Add(node.Parent, list);
            }
            list.Add(entity);
        }
        foreach (EntityId entity in worldEntities)
        {
            Node node = world.Get<Node>(entity);
            children.TryGetValue(entity, out List<EntityId>? expectedChildren);
            if (expectedChildren is null)
            {
                Assert.True(node.FirstChild.IsNone);
                Assert.True(node.LastChild.IsNone);
                continue;
            }
            List<EntityId> actual = [];
            EntityId current = node.FirstChild;
            EntityId previous = EntityId.None;
            int guard = 0;
            while (!current.IsNone)
            {
                Assert.True(guard++ < world.EntityCount + 1, "Child list contains a cycle.");
                Node child = world.Get<Node>(current);
                Assert.Equal(entity, child.Parent);
                Assert.Equal(previous, child.PrevSibling);
                actual.Add(current);
                previous = current;
                current = child.NextSibling;
            }
            Assert.Equal(previous, node.LastChild);
            Assert.Equal(
                expectedChildren.OrderBy(e => e.Index).ThenBy(e => e.Version),
                actual.OrderBy(e => e.Index).ThenBy(e => e.Version));
        }
        Assert.Equal(model.CountWith(typeof(FuzzA)), world.Query<FuzzA>().Count());
        Assert.Equal(model.CountWith(typeof(FuzzB)), world.Query<FuzzB>().Count());
        Assert.Equal(model.CountWith(typeof(FuzzC)), world.Query<FuzzC>().Count());
        Assert.Equal(model.CountWith(typeof(Transform2D)), world.Query<Transform2D>().Count());
        Assert.Equal(
            model.CountWith(typeof(FuzzA), typeof(FuzzB)),
            world.Query<FuzzA, FuzzB>().Count());
        Assert.Equal(
            model.CountWith(typeof(FuzzA), typeof(FuzzC)),
            world.Query<FuzzA>().With<FuzzC>().Count());
        Assert.Equal(
            model.CountWithout(typeof(FuzzB), typeof(FuzzC)),
            world.Query<FuzzB>().Without<FuzzC>().Count());
    }

    private static void ValidateComponents(World world, ReferenceModel model, EntityId entity)
    {
        HashSet<Type> expected = model.Components[entity];
        Assert.Equal(expected.Contains(typeof(FuzzA)), world.Has<FuzzA>(entity));
        Assert.Equal(expected.Contains(typeof(FuzzB)), world.Has<FuzzB>(entity));
        Assert.Equal(expected.Contains(typeof(FuzzC)), world.Has<FuzzC>(entity));
        Assert.Equal(expected.Contains(typeof(Transform2D)), world.Has<Transform2D>(entity));
    }

    private static void ValidateTransform(World world, ReferenceModel model, EntityId entity)
    {
        if (!world.Has<Transform2D>(entity)) return;
        Matrix3x2 expected = Matrix3x2.Identity;
        EntityId current = entity;
        int guard = 0;
        while (!current.IsNone)
        {
            Assert.True(guard++ < world.EntityCount + 1, "Parent chain contains a cycle.");
            if (world.Has<Transform2D>(current)) expected *= world.Get<Transform2D>(current).LocalMatrix;
            current = model.ParentOf(current);
        }
        AssertMatrixNear(expected, world.Get<Transform2D>(entity).World);
    }

    private static Transform2D RandomTransform(Random random) => new()
    {
        Position = new Vector2(
            (float)((random.NextDouble() * 100) - 50),
            (float)((random.NextDouble() * 100) - 50)),
        Rotation = (float)((random.NextDouble() * Math.Tau) - Math.PI),
        Scale = new Vector2(
            (float)(0.5 + random.NextDouble()),
            (float)(0.5 + random.NextDouble())),
    };

    private static void AssertMatrixNear(Matrix3x2 expected, Matrix3x2 actual)
    {
        AssertNear(expected.M11, actual.M11);
        AssertNear(expected.M12, actual.M12);
        AssertNear(expected.M21, actual.M21);
        AssertNear(expected.M22, actual.M22);
        AssertNear(expected.M31, actual.M31);
        AssertNear(expected.M32, actual.M32);
    }

    private static void AssertNear(float expected, float actual)
    {
        float tolerance = 1e-4f * MathF.Max(1f, MathF.Abs(expected));
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    private struct FuzzA : IComponent
    {
        public int Value;
    }

    private struct FuzzB : IComponent
    {
        public int Value;
    }

    private struct FuzzC : IComponent { }

    private sealed class ReferenceModel
    {
        public Dictionary<EntityId, HashSet<Type>> Components { get; } = [];

        public Dictionary<EntityId, EntityId> Parents { get; } = [];

        public Dictionary<EntityId, bool> ActiveSelf { get; } = [];

        public Dictionary<EntityId, HashSet<Type>>.KeyCollection Entities => Components.Keys;

        public void Create(EntityId entity)
        {
            Components.Add(entity, []);
            Parents.Add(entity, EntityId.None);
            ActiveSelf.Add(entity, true);
        }

        public EntityId Pick(Random random)
        {
            int index = random.Next(Components.Count);
            foreach (EntityId entity in Components.Keys) if (index-- == 0) return entity;
            throw new InvalidOperationException("The model is empty.");
        }

        public Type? PickComponent(Random random, EntityId entity)
        {
            HashSet<Type> components = Components[entity];
            if (components.Count == 0) return null;
            int index = random.Next(components.Count);
            foreach (Type type in components) if (index-- == 0) return type;
            throw new InvalidOperationException("Failed to pick a component.");
        }

        public void Add(EntityId entity, Type type) => Components[entity].Add(type);

        public void Remove(EntityId entity, Type type) => Components[entity].Remove(type);

        public EntityId ParentOf(EntityId entity) =>
            Parents.TryGetValue(entity, out EntityId parent) ? parent : EntityId.None;

        public void SetParent(EntityId child, EntityId parent) => Parents[child] = parent;

        public bool WouldCreateCycle(EntityId child, EntityId parent) =>
            !parent.IsNone && IsDescendantOrSelf(parent, child);

        public bool IsDescendantOrSelf(EntityId candidate, EntityId ancestor)
        {
            EntityId current = candidate;
            while (!current.IsNone)
            {
                if (current == ancestor) return true;
                current = ParentOf(current);
            }
            return false;
        }

        public int DepthOf(EntityId entity)
        {
            int depth = 0;
            EntityId current = ParentOf(entity);
            while (!current.IsNone)
            {
                depth++;
                current = ParentOf(current);
            }
            return depth;
        }

        public bool IsActiveInHierarchy(EntityId entity)
        {
            EntityId current = entity;
            while (!current.IsNone)
            {
                if (!ActiveSelf[current]) return false;
                current = ParentOf(current);
            }
            return true;
        }

        public int CountWith(params Type[] types) =>
            Components.Values.Count(set => types.All(set.Contains));

        public int CountWithout(Type included, Type excluded) =>
            Components.Values.Count(set => set.Contains(included) && !set.Contains(excluded));

        public void DestroyReparentingChildren(EntityId entity)
        {
            EntityId parent = ParentOf(entity);
            foreach (EntityId child in Components.Keys.ToList()) if (ParentOf(child) == entity) Parents[child] = parent;
            RemoveEntity(entity);
        }

        public void DestroySubtree(EntityId root)
        {
            List<EntityId> subtree = Components.Keys
                .Where(entity => IsDescendantOrSelf(entity, root))
                .ToList();
            foreach (EntityId entity in subtree) RemoveEntity(entity);
        }

        private void RemoveEntity(EntityId entity)
        {
            Components.Remove(entity);
            Parents.Remove(entity);
            ActiveSelf.Remove(entity);
        }
    }
}
