using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XEngine.Core.Graphics.OpenGL;

namespace XEngine.Core.Common.Sprite.NineSlice
{
                    public class GNineSlice : GSprite
    {
        public Vector4 Borders { get; protected set; } = Vector4.Zero;

                                                                public GNineSlice SetBorders(float left, float right, float top, float bottom)
        {
            Borders = new Vector4(left, right, top, bottom);
            return this;
        }

                                                public GNineSlice SetBorders(float paddingX, float paddingY)
        {
            Borders = new Vector4(paddingX, paddingX, paddingY, paddingY);
            return this;
        }

                                        public GNineSlice SetBorders(float padding)
        {
            Borders = new Vector4(padding, padding, padding, padding);
            return this;
        }

                                        public new GNineSlice SetTexture(Texture2D texture)
        {
            base.SetTexture(texture);
            return this;
        }

                                        public new GNineSlice SetSizingPolicy(SizingPolicy policy)
        {
            base.SetSizingPolicy(policy);
            return this;
        }

                                        public new GNineSlice SetTranslation(Vector2 vec)
        {
            base.SetTranslation(vec);
            return this;
        }

                                        public new GNineSlice SetRotation(float rotation)
        {
            base.SetRotation(rotation);
            return this;
        }

                                        public new GNineSlice SetSize(Vector2 size)
        {
            base.SetSize(size);
            return this;
        }

                                        public new GNineSlice SetAlpha(float alpha)
        {
            base.SetAlpha(alpha);
            return this;
        }

        public Vector2 RenderSize => _size;
    }
}
