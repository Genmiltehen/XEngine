using OpenTK.Mathematics;
using XEngine.Core.Base;

namespace XEngine.Core.Graphics
{
                public sealed class GCamera : GameComponent
    {
                                public void Approach(Vector3 newPos, float strength) => Owner.Transform.Approach(newPos, strength);

                                        public Matrix4 GetViewMatrix() => Matrix4.Invert(Owner.Transform.GetWorldMatrix());
    }
}
