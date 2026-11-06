using System.Numerics;
using XEngine.Common.Transform;
using XEngine.Core.Ecs;
using XEngine.Core.Systems;
using XEngine.Interactive.Camera;

namespace XEngine.Interactive.Render;

public sealed class CircleRenderSystem(SilkGraphicsService graphics, CircleRenderer renderer) : ISystem
{
    private sealed class Collector
    {
        public CircleInstance[] Instances = new CircleInstance[64];
        public int Count;

        public void Add(in Matrix3x2 world, in Circle circle)
        {
            if (Count == Instances.Length) Array.Resize(ref Instances, Instances.Length * 2);

            var scale = MathF.Max(MathF.Abs(world.M11), MathF.Abs(world.M22));
            Instances[Count++] = new CircleInstance
            {
                Center = world.Translation,
                Radius = circle.Radius * scale,
                Color = circle.Color,
            };
        }
    }

    private static readonly StateQueryAction<Collector, Transform2D, Circle> Collect = static (in collector, ref transform, ref circle) => collector.Add(in transform.World, in circle);

    private readonly Collector _collector = new();

    public void Update(World world, float dt)
    {
        _ = dt;
        if (world.Time.FrameCount <= 0) return;

        Vector2 framebuffer = new(graphics.FramebufferSize.X, graphics.FramebufferSize.Y);
        if (framebuffer.X <= 0f || framebuffer.Y <= 0f) return;

        Query<Transform2D, Camera2D> cameraQuery = world.Query<Transform2D, Camera2D>();
        if (cameraQuery.Count() == 0) return;

        EntityId cameraEntity = cameraQuery.EntityAt(0);
        ref Camera2D camera = ref world.Get<Camera2D>(cameraEntity);
        camera.FitTo(framebuffer);
        if (camera.ViewPort.X <= 0f || camera.ViewPort.Y <= 0f) return;

        _collector.Count = 0;
        world.Query<Transform2D, Circle>().ForEach(in _collector, Collect);

        Matrix4x4 viewProjection = new CameraView(in world.Get<Transform2D>(cameraEntity), in camera, framebuffer).ViewProjection;
        renderer.Render(_collector.Instances.AsSpan(0, _collector.Count), in viewProjection, camera.ClearColor, framebuffer);
        graphics.SwapBuffers();
    }
}
