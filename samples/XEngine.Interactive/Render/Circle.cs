using System.Numerics;
using XEngine.Core.Ecs;

namespace XEngine.Interactive.Render;

public struct Circle : IComponent
{
    public float Radius;
    public Vector4 Color;
}
