using System.Numerics;
using XEngine.Core.Ecs;
using XEngine.Core.Systems;
using XEngine.Core.Transforms;

namespace XEngine.Core.Tests.Systems;

public sealed class HeadlessLoopTests
{
    [Fact]
    public void WorldLoop_MovesEntitiesAndDestroysExpiredOnes()
    {
        using var world = new World(w =>
        {
            w.AddSystem(SystemPhase.Update, new MovementSystem());
            w.AddSystem(SystemPhase.PostUpdate, new LifetimeSystem());
        });
        EntityId survivor = world.Create();
        world.Add(survivor, Transform2D.Identity);
        world.Add(survivor, new Velocity { Value = new Vector2(10, -4) });
        EntityId expiring = world.Create();
        world.Add(expiring, Transform2D.At(new Vector2(3, 3)));
        world.Add(expiring, new Lifetime { Seconds = 0.5f });
        for (int frame = 0; frame < 120; frame++) world.Update(1f / 60f);
        Assert.False(world.IsAlive(expiring));
        Assert.True(world.IsAlive(survivor));
        Assert.Equal(120, world.Time.FrameCount);
        Assert.Equal(2f, world.Time.TotalTime, 4);
        Vector2 position = world.Get<Transform2D>(survivor).Position;
        Assert.Equal(20f, position.X, 3);
        Assert.Equal(-8f, position.Y, 3);
    }

    private struct Velocity : IComponent
    {
        public Vector2 Value;
    }

    private struct Lifetime : IComponent
    {
        public float Seconds;
    }

    private sealed class MovementSystem : ISystem
    {
        public void Update(World world, float dt)
        {
            world.Query<Transform2D, Velocity>()
                .ForEach((ref Transform2D transform, ref Velocity velocity) =>
                {
                    transform.Position += velocity.Value * dt;
                });
        }
    }

    private sealed class LifetimeSystem : ISystem
    {
        private CommandBuffer? _commands;

        public void Update(World world, float dt)
        {
            _commands ??= world.BeginCommands();
            CommandBuffer commands = _commands;
            world.Query<Lifetime>().ForEach((EntityId entity, ref Lifetime lifetime) =>
            {
                lifetime.Seconds -= dt;
                if (lifetime.Seconds <= 0f) commands.Destroy(entity);
            });
            world.Submit(commands);
        }
    }
}
