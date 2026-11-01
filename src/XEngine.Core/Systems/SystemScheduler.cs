using XEngine.Core.Ecs;

namespace XEngine.Core.Systems;

internal sealed class SystemScheduler : IDisposable
{
    private static readonly SystemPhase[] PhaseOrder =
    [
        SystemPhase.PreUpdate,
        SystemPhase.Update,
        SystemPhase.PostUpdate,
        SystemPhase.Transform,
    ];

    private readonly List<ISystem>[] _systems;

    public SystemScheduler()
    {
        _systems = new List<ISystem>[PhaseOrder.Length];
        for (var i = 0; i < _systems.Length; i++) _systems[i] = [];
    }

    public void Add(SystemPhase phase, ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown system phase.");
        _systems[(int)phase].Add(system);
    }

    public void Run(SystemPhase phase, World world, float dt)
    {
        List<ISystem> systems = _systems[(int)phase];
        // Регистрация может произойти из работающей системы: новые стартуют со следующего кадра.
        var count = systems.Count;
        for (var i = 0; i < count; i++) systems[i].Update(world, dt);
    }

    public void RunAll(World world, float dt)
    {
        foreach (SystemPhase phase in PhaseOrder) Run(phase, world, dt);
    }

    public void Dispose()
    {
        foreach (List<ISystem> systems in _systems)
        {
            foreach (ISystem system in systems)
                if (system is IDisposable disposable)
                    disposable.Dispose();
            systems.Clear();
        }
    }
}
