using XEngine.Core.Ecs;
using XEngine.Core.Services;

namespace XEngine.Core.Modules;

public interface IEngineModule
{
    void ConfigureComponents(ComponentRegisterContext context) { }
    void ConfigureServices(EngineServiceContainer services) { }
    void ConfigureWorld(World world) { }
}
