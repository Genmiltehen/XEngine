using XEngine.Core.Ecs;
using XEngine.Core.Services;

namespace XEngine.Core.Scenes;

public sealed class SceneBuildContext
{
    internal SceneBuildContext(World world, SceneManager scenes, EngineServiceContainer services, CancellationToken cancellation)
    {
        World = world;
        Scenes = scenes;
        Services = services;
        Cancellation = cancellation;
    }

    public World World { get; }
    public SceneManager Scenes { get; }
    public EngineServiceContainer Services { get; }
    public CancellationToken Cancellation { get; }
}
