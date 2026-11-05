using XEngine.Core.Ecs;
using XEngine.Core.Hierarchy;

namespace XEngine.Core.Modules;

public sealed class CoreModule : IEngineModule
{
    public void ConfigureComponents(ComponentRegisterContext context) => context.Register<Node>("core.Node");
}
