using OpenTK.Windowing.GraphicsLibraryFramework;
using XEngine.Core.Config;

namespace XEngine.Core.Input
{
    public interface IInputConfig
    {
                                        public void Update(float dt);

                                        public void LoadBindingsFromConfig(GameConfig config);

                                                public void BindAction(string actionName, Keys key);

                                        public void SetKeyDown(Keys key);

                                        public void SetKeyUp(Keys key);

                                public void ClearStates();
    }
}
