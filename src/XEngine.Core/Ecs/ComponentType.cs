namespace XEngine.Core.Ecs;

public sealed class ComponentType
{
    internal ComponentType(int id, string name, Type runtimeType, Func<int, IComponentColumn> createColumn)
    {
        Id = id;
        Name = name;
        RuntimeType = runtimeType;
        CreateColumn = createColumn;
    }

    public int Id { get; }
    public string Name { get; }
    public Type RuntimeType { get; }
    internal Func<int, IComponentColumn> CreateColumn { get; }
}
