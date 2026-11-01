using XEngine.Core.Ecs;

namespace XEngine.Core.Scenes;

public sealed class Scene(string name, World world)
{
    public string Name { get; } = name;
    public World World { get; } = world;
    public SceneState State { get; internal set; } = SceneState.Loading;

    internal void Dispose()
    {
        if (State == SceneState.Disposed) return;
        State = SceneState.Unloading;
        World.Dispose();
        State = SceneState.Disposed;
    }
}
