using System.Numerics;
using XEngine.Common.Transform;

namespace XEngine.Interactive.Camera;

public readonly struct CameraView
{
    private readonly Matrix3x2 _worldToView;
    private readonly Matrix3x2 _viewToWorld;
    private readonly Vector2 _viewPort;
    private readonly Vector2 _framebuffer;
    private readonly float _zoom;

    public CameraView(in Transform2D transform, in Camera2D camera, Vector2 framebufferSize)
    {
        if (framebufferSize.X <= 0f || framebufferSize.Y <= 0f) throw new ArgumentException("Framebuffer size must be positive.", nameof(framebufferSize));
        if (camera.ViewPort.X <= 0f || camera.ViewPort.Y <= 0f) throw new ArgumentException("Camera view port must be positive.", nameof(camera));

        _viewPort = camera.ViewPort;
        _framebuffer = framebufferSize;
        _zoom = camera.Zoom > 0f ? camera.Zoom : 1f;
        _viewToWorld = transform.World;
        _worldToView = Matrix3x2.Invert(transform.World, out Matrix3x2 inverse) ? inverse : Matrix3x2.Identity;
    }

    public Vector2 WorldToHiDpi(Vector2 world)
    {
        Vector2 view = Vector2.Transform(world, _worldToView) * _zoom;
        Vector2 normalized = view / (_viewPort * 0.5f);
        Vector2 pixel = (normalized * 0.5f + new Vector2(0.5f)) * _framebuffer;
        pixel.Y = _framebuffer.Y - pixel.Y;
        return pixel;
    }

    public Vector2 HiDpiToWorld(Vector2 hiDpi)
    {
        Vector2 normalized = hiDpi / _framebuffer;
        normalized.Y = 1f - normalized.Y;
        Vector2 view = (normalized * 2f - Vector2.One) * (_viewPort * 0.5f) / _zoom;
        return Vector2.Transform(view, _viewToWorld);
    }

    public Matrix4x4 ViewProjection => ToMatri4x4(_worldToView * Matrix3x2.CreateScale(2f * _zoom / _viewPort.X, 2f * _zoom / _viewPort.Y));

    private static Matrix4x4 ToMatri4x4(Matrix3x2 matrix) => new()
    {
        M11 = matrix.M11,
        M12 = matrix.M12,
        M21 = matrix.M21,
        M22 = matrix.M22,
        M33 = 1f,
        M41 = matrix.M31,
        M42 = matrix.M32,
        M44 = 1f,
    };
}
