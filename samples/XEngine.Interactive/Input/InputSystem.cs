using XEngine.Core.Ecs;
using XEngine.Core.Systems;

namespace XEngine.Interactive.Input;

public sealed class InputSystem(IInputService input) : ISystem
{
    public void Update(World world, float dt)
    {
        _ = world;
        input.Poll(dt);
    }
}
