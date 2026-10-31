using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using XEngine.Core.Common.Transform;
using XEngine.Core.Graphics;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace XEngine.Core.Common.Sprite
{
    public class SpriteRendererModule(GLProvider provider) : RenderModule(provider)
    {
        public override int Priority => 500;

        public override void Render(GScene scene)
        {
            if (_screenSize.X <= 0 || _screenSize.Y <= 0) return;
            var _shader = _provider.GetShader("Sprite");

            _shader.Use();

            _shader.SetMatrix4("uProjection", GetProjectionMatrix(_screenSize));
            _shader.SetMatrix4("uView", scene.Camera.GetViewMatrix());
            _shader.SetInt("uTexture", 0);
            GL.ActiveTexture(TextureUnit.Texture0);

            _provider.UnitQuad.Bind();
            foreach (var (_, tr, sprite) in scene.Query<GTransform, GSprite>())
            {
                if (sprite.Texture == null) continue;

                sprite.UseTexture();

                var flips = Matrix4.CreateScale(sprite.FlipX ? -1 : 1, sprite.FlipY ? -1 : 1, 1);
                var modelMatrix = flips * sprite.GetSize() * sprite.GetModelMatrix() * tr.GetWorldMatrix();
                _shader.SetMatrix4("uModel", modelMatrix);
                _shader.SetFloat("uAlpha", sprite.Alpha);

                _provider.UnitQuad.Draw();
            }

            GL.BindVertexArray(0);
        }
    }
}
