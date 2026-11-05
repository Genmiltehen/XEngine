using XEngine.Core.Ecs;
using XEngine.Core.Hosting;
using XEngine.Core.Services;

namespace XEngine.Core.Modules;

public interface IEngineModule
{
    void ConfigureComponents(ComponentRegisterContext context) { }
    void ConfigureServices(EngineServiceContainer services) { }
    void OnEngineCreated(Engine engine) { }
    void OnEngineDisposing(Engine engine) { }
    void ConfigureWorld(World world) { }
}
