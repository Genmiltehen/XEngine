namespace XEngine.Core.Ecs;

internal sealed class CommandComponent<T>(T value, int typeId) : ICommandComponent where T : struct, IComponent
{
    public int TypeId { get; } = typeId;
    public void Apply(World world, EntityId entity) => world.SetExisting(entity, value);
}
