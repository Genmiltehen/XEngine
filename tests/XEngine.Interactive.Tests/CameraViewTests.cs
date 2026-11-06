using System.Numerics;
using XEngine.Common.Transform;
using XEngine.Interactive.Camera;

namespace XEngine.Interactive.Tests;

public sealed class CameraViewTests
{
    private static readonly Vector2 Framebuffer = new(1600f, 900f);

    [Fact]
    public void WorldToHiDpiMapsCameraPositionToCenter()
    {
        var transform = Transform2D.At(new Vector2(2f, 3f));
        Camera2D camera = Camera2D.Default;

        var view = new CameraView(in transform, in camera, Framebuffer);

        Assert.Equal(new Vector2(800f, 450f), view.WorldToHiDpi(new Vector2(2f, 3f)));
    }

    [Fact]
    public void HiDpiToWorldRoundTrips()
    {
        var transform = Transform2D.At(new Vector2(-1f, 0.5f));
        Camera2D camera = Camera2D.Default;
        var view = new CameraView(in transform, in camera, Framebuffer);

        var world = new Vector2(3.25f, -1.75f);
        Vector2 result = view.HiDpiToWorld(view.WorldToHiDpi(world));

        Assert.Equal(world.X, result.X, 4);
        Assert.Equal(world.Y, result.Y, 4);
    }

    [Fact]
    public void ZoomScalesAroundCameraPosition()
    {
        var transform = Transform2D.At(Vector2.Zero);
        Camera2D camera = Camera2D.Default;
        camera.Zoom = 2f;

        var view = new CameraView(in transform, in camera, Framebuffer);

        Assert.Equal(new Vector2(1600f, 450f), view.WorldToHiDpi(new Vector2(4f, 0f)));
    }

    [Fact]
    public void ViewProjectionMapsWorldToNdc()
    {
        var transform = Transform2D.At(new Vector2(2f, 3f));
        Camera2D camera = Camera2D.Default;
        var view = new CameraView(in transform, in camera, Framebuffer);

        Matrix4x4 matrix = view.ViewProjection;

        var center = Vector4.Transform(new Vector4(2f, 3f, 0f, 1f), matrix);
        Assert.Equal(0f, center.X, 4);
        Assert.Equal(0f, center.Y, 4);

        var right = Vector4.Transform(new Vector4(10f, 3f, 0f, 1f), matrix);
        Assert.Equal(1f, right.X, 4);
        Assert.Equal(0f, right.Y, 4);

        var top = Vector4.Transform(new Vector4(2f, 7.5f, 0f, 1f), matrix);
        Assert.Equal(0f, top.X, 4);
        Assert.Equal(1f, top.Y, 4);
    }

    [Fact]
    public void FitToKeepsHeightAndMatchesAspect()
    {
        Camera2D camera = Camera2D.Default;

        camera.FitTo(new Vector2(1600f, 900f));

        Assert.Equal(9f, camera.ViewPort.Y);
        Assert.Equal(9f * 1600f / 900f, camera.ViewPort.X, 4);
    }

    [Fact]
    public void FitToIgnoresDegenerateFramebuffer()
    {
        Camera2D camera = Camera2D.Default;
        Vector2 before = camera.ViewPort;

        camera.FitTo(Vector2.Zero);

        Assert.Equal(before, camera.ViewPort);
    }
}
