using System.Numerics;
using XEngine.Common.Transform;
using XEngine.Core.Ecs;
using XEngine.Core.Systems;
using XEngine.Interactive.Camera;
using XEngine.Interactive.Input;

namespace XEngine.Interactive.Demo;

public sealed class DemoControlSystem(SilkGraphicsService graphics) : ISystem
{
    private const float ZoomStep = 1.1f;

    public void Update(World world, float dt)
    {
        IInputService input = world.GetResource<IInputService>();
        var scrollDelta = input.ScrollDelta.Y;

        if (!TryGetCamera(world, out EntityId cameraId)) return;

        ref Camera2D camera = ref world.Get<Camera2D>(cameraId);

        Vector2 framebuffer = new(graphics.FramebufferSize.X, graphics.FramebufferSize.Y);
        if (framebuffer.X <= 0f || framebuffer.Y <= 0f) return;
        camera.FitTo(framebuffer);
        if (camera.ViewPort.X <= 0f || camera.ViewPort.Y <= 0f) return;
        if (scrollDelta != 0f) camera.Zoom *= MathF.Pow(ZoomStep, scrollDelta);

        ref Transform2D cameraTransform = ref world.Get<Transform2D>(cameraId);
        cameraTransform.Position += GetDirection(input) * camera.Zoom * dt;

        // var view = new CameraView(in cameraTransform, in camera, framebuffer);
        // world.Get<Transform2D>(cursor).Position = view.HiDpiToWorld(input.MousePosition);
    }

    public bool TryGetCamera(World world, out EntityId cameraId)
    {
        cameraId = EntityId.None;
        Query<Transform2D, Camera2D> cameraQuery = world.Query<Transform2D, Camera2D>();
        if (cameraQuery.Count() == 0) return false;
        cameraId = cameraQuery.EntityAt(0);
        return true;
    }

    public Vector2 GetDirection(IInputService input)
    {
        Vector2 direction = new(input.GetAxis("move.horizontal"), input.GetAxis("move.vertical"));
        if (direction.LengthSquared() >= 1) direction = Vector2.Normalize(direction);
        return direction;
    }
}
