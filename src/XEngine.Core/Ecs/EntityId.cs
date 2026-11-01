namespace XEngine.Core.Ecs;

public readonly record struct EntityId(int Index, int Version)
{
    public static readonly EntityId None = new(-1, 0);
    public bool IsNone => Index < 0;

    public override string ToString() => IsNone ? "Entity(None)" : $"Entity({Index}:{Version})";
}
