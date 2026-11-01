namespace XEngine.Core.Ecs;

internal interface ICommandComponent
{
    int TypeId { get; }
    void Apply(World world, EntityId entity);
}
