using XEngine.Core.Ecs;
using XEngine.Core.Modules;
using XEngine.Core.Services;

namespace XEngine.Core.Hosting;

public sealed class EngineBuilder
{
    private readonly List<IEngineModule> _modules = [];
    private readonly ComponentRegistry _components;
    private bool _built;

    public EngineBuilder(ComponentRegistry? components = null)
    {
        _components = components ?? new ComponentRegistry();
        _modules.Add(new CoreModule());
    }

    public EngineBuilder AddModule(IEngineModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        _modules.Add(module);
        return this;
    }

    public Engine Build()
    {
        if (_built) throw new InvalidOperationException("EngineBuilder.Build can only be called once.");
        _built = true;

        ComponentBake();
        EngineServiceContainer services = ServicesBake();

        try { return new Engine(_components, services, WorldConfigurator, [.. _modules]); }
        catch
        {
            services.Dispose();
            throw;
        }
    }

    private EngineServiceContainer ServicesBake()
    {
        EngineServiceContainer services = new();
        foreach (IEngineModule module in _modules) Configure(module, nameof(IEngineModule.ConfigureServices), () => module.ConfigureServices(services));
        return services;
    }

    private void ComponentBake()
    {
        var registration = new ComponentRegisterContext(_components);
        foreach (IEngineModule module in _modules) Configure(module, nameof(IEngineModule.ConfigureComponents), () => module.ConfigureComponents(registration));
        _components.Freeze();
    }

    private void WorldConfigurator(World world)
    {
        foreach (IEngineModule module in _modules) Configure(module, nameof(IEngineModule.ConfigureWorld), () => module.ConfigureWorld(world));
    }

    private static void Configure(IEngineModule module, string phase, Action configure)
    {
        try { configure(); }
        catch (Exception exception) { throw new InvalidOperationException($"Module {module.GetType().Name} failed in {phase}.", exception); }
    }
}
