using XEngine.Core.Ecs;
using XEngine.Core.Services;
using XEngine.Core.Systems;

namespace XEngine.Core.Scenes;

public sealed class SceneManager(EngineServiceContainer? services = null, WorldConfigurator? configurator = null) : IDisposable
{
    private readonly Stack<Entry> _stack = new();
    private readonly Queue<Transition> _pending = new();
    private readonly WorldConfigurator? _configurator = configurator;
    private readonly bool _ownsServices = services is null;
    private bool _isUpdating;
    private bool _disposed;

    public EngineServiceContainer Services { get; } = services ?? new EngineServiceContainer();
    public Scene? Current => _stack.Count == 0 ? null : _stack.Peek().Scene;
    public int SceneCount => _stack.Count;

    public void Replace(ISceneBuilder builder, string name = "")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ThrowIfDisposed();
        _pending.Enqueue(Transition.Replace(builder, name));
    }

    public void Push(ISceneBuilder builder, string name = "")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ThrowIfDisposed();
        _pending.Enqueue(Transition.Push(builder, name));
    }

    public void Pop()
    {
        ThrowIfDisposed();
        _pending.Enqueue(Transition.Pop());
    }

    public void Reload()
    {
        ThrowIfDisposed();
        _pending.Enqueue(Transition.Reload());
    }

    public void Update(float dt)
    {
        ThrowIfDisposed();
        if (_isUpdating) throw new InvalidOperationException("SceneManager.Update cannot be called recursively.");
        _isUpdating = true;
        try
        {
            // Переходы, запрошенные до кадра, применяются здесь.
            ApplyPending();
            Current?.World.Update(dt);
            // Переходы из этого кадра завершаются в его конце.
            ApplyPending();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void ApplyPending()
    {
        var count = _pending.Count;
        while (count-- > 0) Apply(_pending.Dequeue());
    }

    private void Apply(Transition transition)
    {
        switch (transition.Kind)
        {
            case TransitionKind.Replace:
                ReplaceNow(transition.Builder!, transition.Name!);
                break;
            case TransitionKind.Push:
                PushNow(transition.Builder!, transition.Name!);
                break;
            case TransitionKind.Pop:
                PopNow();
                break;
            case TransitionKind.Reload:
                ReloadNow();
                break;
            default:
                throw new InvalidOperationException($"Unknown scene transition {transition.Kind}.");
        }
    }

    private void ReplaceNow(ISceneBuilder builder, string name)
    {
        Entry entry = BuildScene(builder, name);
        if (_stack.Count == 0)
        {
            _stack.Push(entry);
            return;
        }
        Entry previous = _stack.Pop();
        _stack.Push(entry);
        previous.Scene.Dispose();
    }

    private void PushNow(ISceneBuilder builder, string name)
    {
        Entry entry = BuildScene(builder, name);
        if (_stack.Count > 0) _stack.Peek().Scene.State = SceneState.Paused;
        _stack.Push(entry);
    }

    private void PopNow()
    {
        if (_stack.Count == 0) return;
        _stack.Pop().Scene.Dispose();
        if (_stack.Count > 0) _stack.Peek().Scene.State = SceneState.Active;
    }

    private void ReloadNow()
    {
        if (_stack.Count == 0) throw new InvalidOperationException("Cannot reload because there is no current scene.");
        Entry previous = _stack.Peek();
        Entry entry = BuildScene(previous.Builder, previous.Scene.Name);
        _stack.Pop();
        _stack.Push(entry);
        previous.Scene.Dispose();
    }

    private Entry BuildScene(ISceneBuilder builder, string name)
    {
        World world = new(_configurator);
        Scene scene = new(name, world);
        try
        {
            var context = new SceneBuildContext(world, this, Services, CancellationToken.None);
            builder.Build(context);
            world.UpdateTransforms();
            scene.State = SceneState.Active;
            return new Entry(scene, builder);
        }
        catch
        {
            scene.Dispose();
            throw;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pending.Clear();
        while (_stack.Count > 0) _stack.Pop().Scene.Dispose();
        if (_ownsServices) Services.Dispose();
    }

    private readonly struct Entry(Scene scene, ISceneBuilder builder)
    {
        public Scene Scene { get; } = scene;
        public ISceneBuilder Builder { get; } = builder;
    }

    private enum TransitionKind
    {
        Replace,
        Push,
        Pop,
        Reload,
    }

    private readonly struct Transition(TransitionKind kind, ISceneBuilder? builder, string? name)
    {
        public TransitionKind Kind { get; } = kind;
        public ISceneBuilder? Builder { get; } = builder;
        public string? Name { get; } = name;

        public static Transition Replace(ISceneBuilder builder, string name) => new(TransitionKind.Replace, builder, name);
        public static Transition Push(ISceneBuilder builder, string name) => new(TransitionKind.Push, builder, name);
        public static Transition Pop() => new(TransitionKind.Pop, null, null);
        public static Transition Reload() => new(TransitionKind.Reload, null, null);
    }
}
