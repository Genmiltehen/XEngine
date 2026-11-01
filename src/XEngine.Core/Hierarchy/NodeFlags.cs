namespace XEngine.Core.Hierarchy;

[Flags]
public enum NodeFlags
{
    None = 0,
    ActiveSelf = 1 << 0,
    ActiveInHierarchy = 1 << 1,
    DestroyWithParent = 1 << 2,
}
