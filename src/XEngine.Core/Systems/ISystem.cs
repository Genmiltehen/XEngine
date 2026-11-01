using XEngine.Core.Ecs;

namespace XEngine.Core.Systems;

public interface ISystem
{
    void Update(World world, float dt);
}
