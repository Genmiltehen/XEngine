using XEngine.Core.Ecs;
using XEngine.Core.Modules;

namespace XEngine.Common.Transform;

public sealed class TransformModule : IEngineModule
{
    public void ConfigureComponents(ComponentRegisterContext context) =>
        context.Register<Transform2D>("transform.Transform2D");

    public void ConfigureWorld(World world) => TransformWorldConfigurator.Default(world);
}
