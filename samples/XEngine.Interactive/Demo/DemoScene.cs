using System.Numerics;
using XEngine.Common.Transform;
using XEngine.Core.Ecs;
using XEngine.Core.Scenes;
using XEngine.Core.Systems;
using XEngine.Interactive.Camera;
using XEngine.Interactive.Render;

namespace XEngine.Interactive.Demo;

public sealed class DemoScene : ISceneBuilder
{
    public void Build(SceneBuildContext context)
    {
        World world = context.World;

        EntityId cameraId = world.Create();
        world.Add(cameraId, Transform2D.At(Vector2.Zero));
        world.Add(cameraId, Camera2D.Default);

        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
        {
            EntityId dot = world.Create();
            world.Add(dot, Transform2D.At(new Vector2(x - 4.5f, y - 4.5f)));
            world.Add(dot, new Circle { Radius = 0.08f, Color = new Vector4(0.3f, 0.35f, 0.42f, 1f) });
        }

        SilkGraphicsService graphics = context.Services.Get<SilkGraphicsService>();
        world.AddSystem(SystemPhase.Update, new DemoControlSystem(graphics));
    }
}
