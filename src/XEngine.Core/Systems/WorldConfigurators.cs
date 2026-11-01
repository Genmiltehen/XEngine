using XEngine.Core.Ecs;
using XEngine.Core.Transforms;

namespace XEngine.Core.Systems;

public static class WorldConfigurators
{
    public static readonly WorldConfigurator Default = static world => world.AddSystem(SystemPhase.Transform, new TransformSystem());
}
