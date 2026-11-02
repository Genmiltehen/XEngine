using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;
using XEngine.Core.Systems;
using XEngine.Core.Transforms;

namespace XEngine.Core.Modules;

public sealed class CoreModule : IEngineModule
{
    public void ConfigureComponents(ComponentRegisterContext context)
    {
        context.Register<Node>("core.Node");
        context.Register<Transform2D>("core.Transform2D");
    }

    public void ConfigureWorld(World world) => WorldConfigurators.Default(world);
}
