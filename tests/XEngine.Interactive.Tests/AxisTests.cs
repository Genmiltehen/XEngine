using System.Numerics;
using Silk.NET.Input;
using XEngine.Interactive.Input;
using InputAxis = XEngine.Interactive.Input.Axis;

namespace XEngine.Interactive.Tests;

public sealed class AxisTests
{
    [Fact]
    public void ValueMovesTowardTargetAtSensitivity()
    {
        var input = new StubInput { Active = "right" };
        var axis = new InputAxis(new AxisSettings { Positive = "right", Negative = "left", Sensitivity = 3f, Gravity = 3f });

        axis.Update(input, 0.1f);

        Assert.Equal(0.3f, axis.Value, 4);
    }

    [Fact]
    public void ValueFallsBackToZeroAtGravity()
    {
        var input = new StubInput { Active = "right" };
        var axis = new InputAxis(new AxisSettings { Positive = "right", Negative = "left", Sensitivity = 3f, Gravity = 3f });
        axis.Update(input, 0.1f);

        input.Active = "";
        axis.Update(input, 0.05f);

        Assert.Equal(0.15f, axis.Value, 4);
    }

    [Fact]
    public void OppositeActionsCancelOut()
    {
        var input = new StubInput { Active = "both" };
        var axis = new InputAxis(new AxisSettings { Positive = "right", Negative = "left" });

        axis.Update(input, 1f);

        Assert.Equal(0f, axis.Value);
    }

    private sealed class StubInput : IInputService
    {
        public string Active { get; set; } = "";

        public bool IsActionActive(string action) =>
            Active == "both" || Active == action;

        public void Poll(float dt) => throw new NotSupportedException();
        public bool IsKeyDown(Key key) => throw new NotSupportedException();
        public bool WasKeyPressed(Key key) => throw new NotSupportedException();
        public bool WasKeyReleased(Key key) => throw new NotSupportedException();
        public bool IsMouseDown(MouseButton button) => throw new NotSupportedException();
        public bool WasMousePressed(MouseButton button) => throw new NotSupportedException();
        public bool WasMouseReleased(MouseButton button) => throw new NotSupportedException();
        public Vector2 MousePosition => throw new NotSupportedException();
        public Vector2 MouseDelta => throw new NotSupportedException();
        public Vector2 ScrollDelta => throw new NotSupportedException();
        public bool IsActionInactive(string action) => throw new NotSupportedException();
        public bool IsActionJustPressed(string action) => throw new NotSupportedException();
        public bool IsActionJustReleased(string action) => throw new NotSupportedException();
        public float GetAxis(string axis) => throw new NotSupportedException();
    }
}
