namespace XEngine.Core.Ecs;

public sealed class ComponentRegisterContext
{
    private readonly ComponentRegistry _registry;
    internal ComponentRegisterContext(ComponentRegistry registry) => _registry = registry;
    public ComponentType Register<T>(string name) where T : struct, IComponent => _registry.Register<T>(name);
    public ComponentType Get<T>() where T : struct, IComponent => _registry.Get<T>();
    public bool TryGet<T>(out ComponentType type) where T : struct, IComponent => _registry.TryGet<T>(out type);
}
