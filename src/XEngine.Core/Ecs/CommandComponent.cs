namespace XEngine.Core.Ecs;

internal sealed class CommandComponent<T>(T value) : ICommandComponent where T : struct, IComponent
{
    public int TypeId => ComponentType<T>.Id;

    public void Apply(World world, EntityId entity) => world.SetExisting(entity, value);
}
