using OpenTK.Mathematics;
using XEngine.Core.Utils.JSONConverters;

namespace XEngine.Core.Common.Transform
{
    public class TransformValues
    {
        [JsonVector3] public Vector3 Position { get; set; } = Vector3.Zero;

        public float Rotation { get; set; } = 0;
    }
}
