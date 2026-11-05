using XEngine.Core.Ecs;
using XEngine.Core.Systems;

namespace XEngine.Common.Transform;

public static class TransformWorldConfigurator
{
    public static readonly WorldConfigurator Default = static world =>
    {
        world.AddSystem(SystemPhase.Transform, new TransformSystem());
        world.BeforeReparent += TransformReparentListener.OnBeforeReparent;
    };
}
