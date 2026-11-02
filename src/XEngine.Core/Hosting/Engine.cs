using XEngine.Core.Ecs;
using XEngine.Core.Modules;
using XEngine.Core.Scenes;
using XEngine.Core.Services;
using XEngine.Core.Systems;

namespace XEngine.Core.Hosting;

public sealed class Engine : IDisposable
{
    private readonly IEngineModule[] _modules;
    private bool _disposed;

    internal Engine(ComponentRegistry components, EngineServiceContainer services, WorldConfigurator configurator, IEngineModule[] modules)
    {
        Components = components;
        Services = services;
        Scenes = new SceneManager(components, services, configurator);
        _modules = modules;
    }

    public ComponentRegistry Components { get; }
    public EngineServiceContainer Services { get; }
    public SceneManager Scenes { get; }

    public void Update(float dt) => Scenes.Update(dt);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Scenes.Dispose();
        foreach (IEngineModule module in _modules)
            if (module is IDisposable disposable) disposable.Dispose();
        Services.Dispose();
    }
}
