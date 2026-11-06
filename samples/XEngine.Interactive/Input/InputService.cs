using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace XEngine.Interactive.Input;

public sealed class InputService(SilkGraphicsService graphics) : IInputService
{
    private static readonly Key[] AllKeys = Enum.GetValues<Key>();
    private static readonly MouseButton[] AllButtons = Enum.GetValues<MouseButton>();

    private readonly Dictionary<string, List<Key>> _actionBindings = [];
    private readonly Dictionary<string, Axis> _axes = [];
    private readonly HashSet<Key> _downKeys = [];
    private readonly HashSet<Key> _previousDownKeys = [];
    private readonly HashSet<MouseButton> _downButtons = [];
    private readonly HashSet<MouseButton> _previousDownButtons = [];

    private readonly ScrollAccumulator _scroll = new();

    private IInputContext? _input;
    private IMouse? _mouse;
    private Vector2 _previousMousePosition;
    private bool _firstPoll = true;

    public Vector2 MousePosition { get; private set; }
    public Vector2 MouseDelta { get; private set; }
    public Vector2 ScrollDelta { get; private set; }

    public void Attach(IInputContext input)
    {
        Detach();
        _input = input;
        EnsureMouse(input);
    }

    public void Detach()
    {
        if (_mouse is not null) _mouse.Scroll -= OnScroll;
        _mouse = null;
        _input = null;
        _scroll.Reset();
        ScrollDelta = Vector2.Zero;
    }

    public void BindAction(string action, Key key)
    {
        if (!_actionBindings.TryGetValue(action, out List<Key>? keys))
        {
            keys = [];
            _actionBindings.Add(action, keys);
        }
        if (!keys.Contains(key)) keys.Add(key);
    }

    public void AddAxis(string name, AxisSettings settings) => _axes[name] = new Axis(settings);

    public bool IsKeyDown(Key key) => _downKeys.Contains(key);

    public bool WasKeyPressed(Key key) => _downKeys.Contains(key) && !_previousDownKeys.Contains(key);

    public bool WasKeyReleased(Key key) => !_downKeys.Contains(key) && _previousDownKeys.Contains(key);

    public bool IsMouseDown(MouseButton button) => _downButtons.Contains(button);

    public bool WasMousePressed(MouseButton button) => _downButtons.Contains(button) && !_previousDownButtons.Contains(button);

    public bool WasMouseReleased(MouseButton button) => !_downButtons.Contains(button) && _previousDownButtons.Contains(button);

    public bool IsActionInactive(string action)
    {
        if (!_actionBindings.TryGetValue(action, out List<Key>? keys)) return false;
        foreach (Key key in keys) if (_downKeys.Contains(key) || _previousDownKeys.Contains(key)) return false;
        return true;
    }

    public bool IsActionActive(string action)
    {
        if (!_actionBindings.TryGetValue(action, out List<Key>? keys)) return false;
        foreach (Key key in keys) if (_downKeys.Contains(key) && _previousDownKeys.Contains(key)) return true;
        return false;
    }

    public bool IsActionJustPressed(string action)
    {
        if (!_actionBindings.TryGetValue(action, out List<Key>? keys)) return false;
        foreach (Key key in keys) if (_downKeys.Contains(key) && !_previousDownKeys.Contains(key)) return true;
        return false;
    }

    public bool IsActionJustReleased(string action)
    {
        if (!_actionBindings.TryGetValue(action, out List<Key>? keys)) return false;
        foreach (Key key in keys) if (!_downKeys.Contains(key) && _previousDownKeys.Contains(key)) return true;
        return false;
    }

    public float GetAxis(string axis) => _axes.TryGetValue(axis, out Axis? value) ? value.Value : 0f;

    public void Poll(float dt)
    {
        IInputContext? input = _input;
        if (input is null) return;
        EnsureMouse(input);

        _previousDownKeys.Clear();
        _previousDownKeys.UnionWith(_downKeys);
        _downKeys.Clear();

        _previousDownButtons.Clear();
        _previousDownButtons.UnionWith(_downButtons);
        _downButtons.Clear();

        if (input.Keyboards.Count > 0)
        {
            IKeyboard keyboard = input.Keyboards[0];
            foreach (Key key in AllKeys) if (keyboard.IsKeyPressed(key)) _downKeys.Add(key);
        }

        ScrollDelta = _scroll.Consume();

        if (input.Mice.Count > 0)
        {
            IMouse mouse = input.Mice[0];
            foreach (MouseButton button in AllButtons) if (mouse.IsButtonPressed(button)) _downButtons.Add(button);

            Vector2 position = mouse.Position * ComputeScale();
            MouseDelta = position - _previousMousePosition;
            MousePosition = position;
            _previousMousePosition = position;
        }
        else
        {
            MouseDelta = Vector2.Zero;
            ScrollDelta = Vector2.Zero;
            _scroll.Reset();
        }

        if (_firstPoll)
        {
            _previousDownKeys.UnionWith(_downKeys);
            _previousDownButtons.UnionWith(_downButtons);
            _previousMousePosition = MousePosition;
            MouseDelta = Vector2.Zero;
            ScrollDelta = Vector2.Zero;
            _scroll.Reset();
            _firstPoll = false;
        }

        foreach (Axis axis in _axes.Values) axis.Update(this, dt);
    }

    private void OnScroll(IMouse mouse, ScrollWheel wheel)
    {
        _ = mouse;
        _scroll.Add(new Vector2(wheel.X, wheel.Y));
    }

    private void EnsureMouse(IInputContext input)
    {
        IMouse? mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
        if (ReferenceEquals(mouse, _mouse)) return;
        if (_mouse is not null) _mouse.Scroll -= OnScroll;
        _mouse = mouse;
        if (_mouse is not null) _mouse.Scroll += OnScroll;
    }

    private Vector2 ComputeScale()
    {
        Vector2D<int> framebuffer = graphics.FramebufferSize;
        Vector2D<int> window = graphics.WindowSize;
        return window.X <= 0 || window.Y <= 0 || framebuffer.X <= 0 || framebuffer.Y <= 0
            ? Vector2.One
            : new Vector2((float)framebuffer.X / window.X, (float)framebuffer.Y / window.Y);
    }
}
