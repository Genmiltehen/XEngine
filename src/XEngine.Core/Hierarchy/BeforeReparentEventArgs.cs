using XEngine.Core.Ecs;

namespace XEngine.Core.Hierarchy;

public readonly struct BeforeReparentEventArgs(EntityId child, EntityId previousParent, EntityId newParent)
{
    public EntityId Child { get; } = child;
    public EntityId PreviousParent { get; } = previousParent;
    public EntityId NewParent { get; } = newParent;
}
