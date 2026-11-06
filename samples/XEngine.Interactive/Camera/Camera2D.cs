using System.Numerics;
using XEngine.Core.Ecs;

namespace XEngine.Interactive.Camera;

public struct Camera2D : IComponent
{
    public Vector2 ViewPort;
    public float Zoom;
    public Vector4 ClearColor;

    public static Camera2D Default => new()
    {
        ViewPort = new Vector2(16f, 9f),
        Zoom = 1f,
        ClearColor = new Vector4(0.06f, 0.07f, 0.09f, 1f),
    };

    public void FitTo(Vector2 framebufferSize)
    {
        if (framebufferSize.X <= 0f || framebufferSize.Y <= 0f) return;
        if (ViewPort.Y <= 0f) return;
        ViewPort.X = ViewPort.Y * framebufferSize.X / framebufferSize.Y;
    }
}
