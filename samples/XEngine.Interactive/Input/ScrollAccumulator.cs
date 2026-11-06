using System.Numerics;

namespace XEngine.Interactive.Input;

public sealed class ScrollAccumulator
{
    public Vector2 Pending { get; private set; }

    public void Add(Vector2 delta) => Pending += delta;

    public Vector2 Consume()
    {
        Vector2 value = Pending;
        Pending = Vector2.Zero;
        return value;
    }

    public void Reset() => Pending = Vector2.Zero;
}
