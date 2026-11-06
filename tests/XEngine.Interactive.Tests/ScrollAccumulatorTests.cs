using System.Numerics;
using XEngine.Interactive.Input;

namespace XEngine.Interactive.Tests;

public sealed class ScrollAccumulatorTests
{
    [Fact]
    public void AddAccumulatesDeltas()
    {
        var scroll = new ScrollAccumulator();

        scroll.Add(new Vector2(1f, 2f));
        scroll.Add(new Vector2(-0.5f, 3f));

        Assert.Equal(new Vector2(0.5f, 5f), scroll.Pending);
    }

    [Fact]
    public void ConsumeReturnsAndClearsPending()
    {
        var scroll = new ScrollAccumulator();
        scroll.Add(new Vector2(0f, 2f));

        Vector2 value = scroll.Consume();

        Assert.Equal(new Vector2(0f, 2f), value);
        Assert.Equal(Vector2.Zero, scroll.Pending);
        Assert.Equal(Vector2.Zero, scroll.Consume());
    }

    [Fact]
    public void ResetClearsPending()
    {
        var scroll = new ScrollAccumulator();
        scroll.Add(new Vector2(4f, -1f));

        scroll.Reset();

        Assert.Equal(Vector2.Zero, scroll.Pending);
    }
}
