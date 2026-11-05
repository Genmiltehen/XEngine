namespace XEngine.Core.Ecs;

// Разрешает компонент один раз и передаёт его в горячие пути без ComponentType.
public readonly struct ComponentHandle<T> where T : struct, IComponent
{
    internal ComponentHandle(ComponentType type, ComponentRegistry registry)
    {
        Type = type;
        Registry = registry;
    }

    internal ComponentType Type => field ?? throw new InvalidOperationException("Component handle is not initialized.");
    internal ComponentRegistry Registry => field ?? throw new InvalidOperationException("Component handle is not initialized.");
}
