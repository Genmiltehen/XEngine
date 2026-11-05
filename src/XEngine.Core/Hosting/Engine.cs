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
        foreach (IEngineModule module in _modules) InvokeHook(module, nameof(IEngineModule.OnEngineCreated), () => module.OnEngineCreated(this));
    }

    public ComponentRegistry Components { get; }
    public EngineServiceContainer Services { get; }
    public SceneManager Scenes { get; }

    public void Update(float dt) => Scenes.Update(dt);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Exception? hookFailure = null;
        foreach (IEngineModule module in _modules)
        {
            try { module.OnEngineDisposing(this); }
            catch (Exception exception) { hookFailure ??= WrapHookFailure(module, nameof(IEngineModule.OnEngineDisposing), exception); }
        }
        try
        {
            Scenes.Dispose();
            foreach (IEngineModule module in _modules)
                if (module is IDisposable disposable) disposable.Dispose();
        }
        finally
        {
            Services.Dispose();
        }
        if (hookFailure is not null) throw hookFailure;
    }

    private static void InvokeHook(IEngineModule module, string hook, Action action)
    {
        try { action(); }
        catch (Exception exception) { throw WrapHookFailure(module, hook, exception); }
    }

    private static InvalidOperationException WrapHookFailure(IEngineModule module, string hook, Exception exception) =>
        new($"Module {module.GetType().Name} failed in {hook}.", exception);
}
