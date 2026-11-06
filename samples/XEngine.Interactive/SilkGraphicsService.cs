using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace XEngine.Interactive;

public sealed class SilkGraphicsService(SilkTestOptions options) : IDisposable
{
    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;
    private bool _disposed;

    public GL GL => _gl ?? throw new InvalidOperationException("Graphics are not initialized.");
    public IInputContext Input => _input ?? throw new InvalidOperationException("Input is not initialized.");
    public Vector2D<int> FramebufferSize => _window?.FramebufferSize ?? Vector2D<int>.Zero;
    public Vector2D<int> WindowSize => _window?.Size ?? Vector2D<int>.Zero;
    public bool IsClosing => _window?.IsClosing ?? true;

    public void Initialize()
    {
        if (_window is not null) return;

        WindowOptions windowOptions = WindowOptions.Default with
        {
            Title = options.Title,
            Size = new Vector2D<int>(options.Width, options.Height),
            VSync = options.VSync,
            ShouldSwapAutomatically = false,
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3)),
        };

        _window = Window.Create(windowOptions);
        _window.Initialize();
        
        _gl = GL.GetApi(_window);
        _input = _window.CreateInput();
    }

    public void PumpEvents() => _window?.DoEvents();

    public void SwapBuffers() => _window?.SwapBuffers();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _input?.Dispose();
        _input = null;
        _gl?.Dispose();
        _gl = null;
        _window?.Dispose();
        _window = null;
    }
}
