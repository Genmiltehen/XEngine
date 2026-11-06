using Silk.NET.Input;
using XEngine.Core.Ecs;
using XEngine.Core.Hosting;
using XEngine.Core.Modules;
using XEngine.Core.Services;
using XEngine.Core.Systems;
using XEngine.Interactive.Camera;
using XEngine.Interactive.Input;
using XEngine.Interactive.Render;

namespace XEngine.Interactive;

public sealed class SilkTestModule : IEngineModule, IDisposable
{
    private readonly SilkGraphicsService _graphics;
    private readonly InputService _input;
    private readonly CircleRenderer _renderer;
    private bool _disposed;

    public SilkTestModule(SilkTestOptions options)
    {
        _graphics = new SilkGraphicsService(options);
        _input = new InputService(_graphics);
        _renderer = new CircleRenderer(_graphics);
        ConfigureInput();
    }

    private void ConfigureInput()
    {
        _input.BindAction("move.left", Key.A);
        _input.BindAction("move.right", Key.D);
        _input.BindAction("move.up", Key.W);
        _input.BindAction("move.down", Key.S);
        _input.BindAction("reset", Key.Space);

        _input.AddAxis("move.horizontal", new AxisSettings { Positive = "move.right", Negative = "move.left", Sensitivity = 10, Gravity = 10 });
        _input.AddAxis("move.vertical", new AxisSettings { Positive = "move.up", Negative = "move.down", Sensitivity = 10, Gravity = 10 });
    }

    public void ConfigureComponents(ComponentRegisterContext context)
    {
        context.Register<Circle>("sample.Circle");
        context.Register<Camera2D>("sample.Camera2D");
    }

    public void ConfigureServices(EngineServiceContainer services)
    {
        services.Set(_graphics);
        services.Set<IInputService>(_input);
    }

    public void OnEngineCreated(Engine engine)
    {
        _ = engine;
        _graphics.Initialize();
        _renderer.Initialize();
        _input.Attach(_graphics.Input);
    }

    public void ConfigureWorld(World world)
    {
        world.SetResource<IInputService>(_input);
        world.AddSystem(SystemPhase.PreUpdate, new InputSystem(_input));
        world.AddSystem(SystemPhase.Transform, new CircleRenderSystem(_graphics, _renderer));
    }

    public void OnEngineDisposing(Engine engine)
    {
        _ = engine;
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _renderer.Dispose();
        _input.Detach();
    }
}
