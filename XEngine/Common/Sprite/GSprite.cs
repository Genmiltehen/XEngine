using OpenTK.Mathematics;
using XEngine.Core.Base;
using XEngine.Core.Common.Sprite.NineSlice;
using XEngine.Core.Graphics.OpenGL;

namespace XEngine.Core.Common.Sprite
{
    public class GSprite : GameComponent
    {
        public Texture2D Texture { get; set; } = null!;

        protected Vector2 _position = Vector2.Zero;
        protected Vector2 _size = Vector2.One;
        protected float _rotation = 0f;

        protected Matrix4 _modelMatrix = Matrix4.Identity;
        protected bool _isDirty = true;

        public float Alpha { get; protected set; } = 1;
        public SizingPolicy SizingPolicy { get; protected set; }
        public bool FlipX = false;
        public bool FlipY = false;

        public GSprite SetTexture(Texture2D texture)
        {
            Texture = texture;
            _isDirty = true;
            return this;
        }

        public GSprite SetSizingPolicy(SizingPolicy policy)
        {
            SizingPolicy = policy;
            return this;
        }

        public GSprite SetTranslation(Vector2 vec)
        {
            _position = vec;
            _isDirty = true;
            return this;
        }

        public GSprite SetRotation(float rotation)
        {
            _rotation = rotation;
            _isDirty = true;
            return this;
        }

        public GSprite SetSize(Vector2 size)
        {
            _size = size;
            _isDirty = true;
            return this;
        }

        public GSprite SetAlpha(float alpha)
        {
            Alpha = alpha;
            return this;
        }

        public Matrix4 GetModelMatrix()
        {
            if (_isDirty) Recalculate();
            return _modelMatrix;
        }

        public Matrix4 GetSize(float ppm = 1) => SizingPolicy switch
        {
            SizingPolicy.Identity => Matrix4.CreateScale(_size.X, _size.Y, 1),
            SizingPolicy.World => Matrix4.CreateScale(_size.X * ppm, _size.Y * ppm, 1),
            SizingPolicy.Source => Matrix4.CreateScale(TextureSize.X * _size.X, TextureSize.Y * _size.X, 1),
            _ => Matrix4.Identity,
        };

        protected void Recalculate()
        {
            if (Texture == null)
            {
                _modelMatrix = Matrix4.Identity;
                _isDirty = false;
                return;
            }

            var rotMat = Matrix4.CreateRotationZ(_rotation);
            var transMat = Matrix4.CreateTranslation(_position.X, _position.Y, 0f);

            _modelMatrix = rotMat * transMat;
            _isDirty = false;
        }

        public void UseTexture() => Texture.Use();

        public Vector2 TextureSize => new(Texture.Width, Texture.Height);
    }
}
