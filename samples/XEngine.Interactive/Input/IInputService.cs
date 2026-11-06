using System.Numerics;
using Silk.NET.Input;

namespace XEngine.Interactive.Input;

public interface IInputService
{
    void Poll(float dt);

    bool IsKeyDown(Key key);
    bool WasKeyPressed(Key key);
    bool WasKeyReleased(Key key);

    bool IsMouseDown(MouseButton button);
    bool WasMousePressed(MouseButton button);
    bool WasMouseReleased(MouseButton button);

    Vector2 MousePosition { get; }
    Vector2 MouseDelta { get; }
    Vector2 ScrollDelta { get; }

    bool IsActionInactive(string action);
    bool IsActionActive(string action);
    bool IsActionJustPressed(string action);
    bool IsActionJustReleased(string action);

    float GetAxis(string axis);
}
