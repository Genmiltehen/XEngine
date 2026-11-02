using XEngine.Core.Ecs;
using XEngine.Core.Modules;
using XEngine.Core.Services;
using XEngine.Core.Systems;

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

        var registration = new ComponentRegisterContext(_components);
        foreach (IEngineModule module in _modules) Configure(module, nameof(IEngineModule.ConfigureComponents), () => module.ConfigureComponents(registration));
        _components.Freeze();

        EngineServiceContainer services = new();
        foreach (IEngineModule module in _modules) Configure(module, nameof(IEngineModule.ConfigureServices), () => module.ConfigureServices(services));

        void configurator(World world)
        {
            foreach (IEngineModule module in _modules) Configure(module, nameof(IEngineModule.ConfigureWorld), () => module.ConfigureWorld(world));
        }

        return new Engine(_components, services, configurator, [.. _modules]);
    }

    private static void Configure(IEngineModule module, string phase, Action configure)
    {
        try { configure(); }
        catch (Exception exception) { throw new InvalidOperationException($"Module {module.GetType().Name} failed in {phase}.", exception); }
    }
}
