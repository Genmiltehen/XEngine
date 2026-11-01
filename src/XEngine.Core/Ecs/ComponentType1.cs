namespace XEngine.Core.Ecs;

public sealed class ComponentType<T> : ComponentType where T : struct, IComponent
{
    public static ComponentType<T> Instance { get; } = new();
    public new static int Id => ((ComponentType)Instance).Id;

    private ComponentType() : base(typeof(T)) { }

    internal override IComponentColumn CreateColumn(int capacity) => new Column<T>(capacity);
}
