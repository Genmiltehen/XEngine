using XEngine.Core.Scenery;

namespace XEngine.Core.Base
{
    public interface IGameSystem
    {
        public int Priority { get; }

        public bool IsEnabled { get; set; }

        void Update(GScene _scene, float _dt);
    }
}
