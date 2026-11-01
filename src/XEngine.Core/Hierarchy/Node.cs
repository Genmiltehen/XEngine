using XEngine.Core.Ecs;

namespace XEngine.Core.Hierarchy;

public struct Node : IComponent
{
    public EntityId Parent;
    public EntityId FirstChild;
    public EntityId LastChild;
    public EntityId NextSibling;
    public EntityId PrevSibling;
    public NodeFlags Flags;
    public int Depth;

    public readonly bool ActiveSelf => (Flags & NodeFlags.ActiveSelf) != 0;
    public readonly bool ActiveInHierarchy => (Flags & NodeFlags.ActiveInHierarchy) != 0;

    internal static Node New() => new()
    {
        Parent = EntityId.None,
        FirstChild = EntityId.None,
        LastChild = EntityId.None,
        NextSibling = EntityId.None,
        PrevSibling = EntityId.None,
        Flags = NodeFlags.ActiveSelf | NodeFlags.ActiveInHierarchy,
        Depth = 0,
    };
}
