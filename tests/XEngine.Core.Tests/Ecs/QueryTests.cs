using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;

namespace XEngine.Core.Tests.Ecs;

public sealed class QueryTests
{
    private static readonly int[] ExpectedValues = [1, 2, 3];

    [Fact]
    public void ForEach_OneComponent_VisitsOnlyMatchingEntities()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        world.Create();
        world.Add(first, new Health { Value = 1 });
        world.Add(second, new Health { Value = 2 });
        Assert.Equal(2, world.Query<Health>().Count());
        int sum = 0;
        world.Query<Health>().ForEach((ref Health health) => sum += health.Value);
        Assert.Equal(3, sum);
    }

    [Fact]
    public void ForEach_EntityOverload_ReportsCorrectIds()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        world.Add(first, new Health { Value = 1 });
        world.Add(second, new Health { Value = 2 });
        List<EntityId> visited = [];
        world.Query<Health>().ForEach((EntityId entity, ref Health health) =>
        {
            _ = health;
            visited.Add(entity);
        });
        Assert.Equal(2, visited.Count);
        Assert.Contains(first, visited);
        Assert.Contains(second, visited);
    }

    [Fact]
    public void ForEach_TwoComponents_UpdatesThroughRef()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 10 });
        world.Add(entity, new Velocity { Value = new System.Numerics.Vector2(2, 3) });
        world.Query<Health, Velocity>().ForEach((ref Health health, ref Velocity velocity) =>
        {
            health.Value += (int)velocity.Value.X;
        });
        Assert.Equal(12, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void ForEach_ThreeComponents_AndFiltersWork()
    {
        using var world = new World();
        EntityId full = world.Create();
        EntityId active = world.Create();
        EntityId frozen = world.Create();
        foreach (EntityId entity in new[] { full, active, frozen })
        {
            world.Add(entity, new Health());
            world.Add(entity, new Velocity());
        }
        world.Add(full, new TagA());
        world.Add(active, new TagA());
        world.Add(active, new TagB());
        world.Add(frozen, new TagA());
        world.Add(frozen, new TagC());
        int withA = 0;
        world.Query<Health, Velocity, TagA>().ForEach(
            (ref Health health, ref Velocity velocity, ref TagA tag) =>
            {
                _ = health;
                _ = velocity;
                _ = tag;
                withA++;
            });
        int withoutC = 0;
        world.Query<Health, Velocity, TagA>()
            .Without<TagC>()
            .ForEach((ref Health health, ref Velocity velocity, ref TagA tag) =>
            {
                _ = health;
                _ = velocity;
                _ = tag;
                withoutC++;
            });
        int withB = 0;
        world.Query<Health>()
            .With<TagB>()
            .ForEach((ref Health health) =>
            {
                _ = health;
                withB++;
            });
        Assert.Equal(3, withA);
        Assert.Equal(2, withoutC);
        Assert.Equal(1, withB);
    }

    [Fact]
    public void Query_SpansMultipleArchetypesInCreationOrder()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId third = world.Create();
        world.Add(first, new Health { Value = 1 });
        world.Add(second, new Health { Value = 2 });
        world.Add(second, new Velocity());
        world.Add(third, new Health { Value = 3 });
        world.Add(third, new TagA());
        List<int> values = [];
        world.Query<Health>().ForEach((ref Health health) => values.Add(health.Value));
        Assert.Equal(ExpectedValues, values);
    }

    [Fact]
    public void EntityAt_ReturnsElementsInArchetypeOrder_AndHonorsFilters()
    {
        using var world = new World();
        EntityId first = world.Create();
        EntityId second = world.Create();
        EntityId third = world.Create();
        world.Add(first, new Health { Value = 1 });
        world.Add(second, new Health { Value = 2 });
        world.Add(second, new Velocity());
        world.Add(third, new Health { Value = 3 });
        world.Add(third, new TagA());

        Assert.Equal(first, world.Query<Health>().EntityAt());
        Assert.Equal(second, world.Query<Health>().EntityAt(1));
        Assert.Equal(third, world.Query<Health>().EntityAt(2));
        Assert.Equal(second, world.Query<Health>().With<Velocity>().EntityAt());
        Assert.Equal(third, world.Query<Health>().Without<Velocity>().EntityAt(1));
        Assert.Equal(second, world.Query<Health, Velocity, Node>().EntityAt());
    }

    [Fact]
    public void EntityAt_OutOfRange_Throws()
    {
        using var world = new World();
        world.Add(world.Create(), new Health());

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Query<Health>().EntityAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Query<Health>().EntityAt(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Query<Velocity>().EntityAt());
    }

    [Fact]
    public void ForEach_WithState_DoesNotCaptureDelegate()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 5 });
        var state = new HealthBonus { Amount = 7 };
        world.Query<Health>().ForEach(in state, static (in HealthBonus bonus, ref Health health) =>
        {
            health.Value += bonus.Amount;
        });
        Assert.Equal(12, world.Get<Health>(entity).Value);
    }

    [Fact]
    public void ForEach_WithEntityAndState_Works()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 5 });
        world.Add(entity, new Velocity());
        List<EntityId> visited = [];
        var state = new Collector { Entities = visited };
        world.Query<Health, Velocity>().ForEach(
            in state,
            static (in Collector collector, EntityId current, ref Health health, ref Velocity velocity) =>
            {
                _ = health;
                _ = velocity;
                collector.Entities.Add(current);
            });
        Assert.Single(visited);
        Assert.Equal(entity, visited[0]);
    }

#if DEBUG
    [Fact]
    public void StructuralChangeInsideForEach_Throws()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health());
        Assert.Throws<InvalidOperationException>(() =>
            world.Query<Health>().ForEach((EntityId current, ref Health health) =>
            {
                _ = health;
                world.Add(current, new Velocity());
            }));
        Assert.True(world.IsAlive(entity));
        Assert.False(world.Has<Velocity>(entity));
    }
#endif

    [Fact]
    public void QueryIteration_CanBeNestedWithoutStructuralChanges()
    {
        using var world = new World();
        EntityId entity = world.Create();
        world.Add(entity, new Health { Value = 2 });
        world.Add(entity, new Velocity { Value = new System.Numerics.Vector2(1, 1) });
        int visits = 0;
        world.Query<Health>().ForEach((ref Health health) =>
        {
            _ = health;
            world.Query<Velocity>().ForEach((ref Velocity velocity) =>
            {
                _ = velocity;
                visits++;
            });
        });
        Assert.Equal(1, visits);
    }

    [Fact]
    public void ForEach_DoesNotAllocateAfterWarmup()
    {
        using var world = new World();
        for (int i = 0; i < 100; i++)
        {
            EntityId entity = world.Create();
            world.Add(entity, new Health { Value = i });
        }
        static void Iterate(World target)
        {
            for (int i = 0; i < 1000; i++) target.Query<Health>().ForEach(static (ref Health health) => health.Value++);
        }
        // Run a full block first so tiering and lazy delegate initialization are done.
        Iterate(world);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Iterate(world);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private struct HealthBonus
    {
        public int Amount;
    }

    private struct Collector
    {
        public List<EntityId> Entities;
    }
}
