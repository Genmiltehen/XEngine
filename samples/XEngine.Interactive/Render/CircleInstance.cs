using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace XEngine.Interactive.Render;

[StructLayout(LayoutKind.Sequential)]
public struct CircleInstance
{
    public const int SizeInBytes = 28;
    public const int CenterOffset = 0;
    public const int RadiusOffset = 8;
    public const int ColorOffset = 12;

    public Vector2 Center;
    public float Radius;
    public Vector4 Color;

    static CircleInstance()
    {
        Debug.Assert(Unsafe.SizeOf<CircleInstance>() == SizeInBytes, "CircleInstance layout changed.");
    }
}
