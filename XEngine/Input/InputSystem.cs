using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace XEngine.Core.Input
{
                public abstract class InputSystem(IInputService input) : IGameSystem
    {
        protected readonly IInputService input = input;
        public int Priority => 100;
        public bool IsEnabled { get; set; } = true;
        public abstract void Update(GScene _scene, float _dt);
    }
}
