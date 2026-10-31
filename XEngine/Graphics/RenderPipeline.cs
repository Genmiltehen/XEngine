using OpenTK.Graphics.OpenGL4;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;


namespace XEngine.Core.Graphics
{
                public class RenderPipeline
    {
        private readonly List<RenderModule> _renderModules = [];

                                public void AddRenderModule(RenderModule renderModule)
        {
            _renderModules.Add(renderModule);
            _renderModules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

                                public void OnResize(int width, int height)
        {
            GL.Viewport(0, 0, width, height);
            foreach (var _r in _renderModules) _r.OnResize(width, height);
        }

                                        public void Render(GScene scene)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            foreach (var _r in _renderModules) if (_r.IsEnabled) _r.Render(scene);
        }
    }
}
