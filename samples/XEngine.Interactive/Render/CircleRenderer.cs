using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace XEngine.Interactive.Render;

public sealed class CircleRenderer(SilkGraphicsService graphics) : IDisposable
{
    private const string VertexShader = """
        #version 330 core

        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aCenter;
        layout(location = 2) in float aRadius;
        layout(location = 3) in vec4 aColor;

        uniform mat4 uViewProjection;

        out vec2 vLocal;
        out vec4 vColor;

        void main()
        {
            vLocal = aPos;
            vColor = aColor;
            gl_Position = uViewProjection * vec4(aCenter + aPos * aRadius, 0.0, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 330 core

        in vec2 vLocal;
        in vec4 vColor;
        out vec4 FragColor;

        void main()
        {
            float d = length(vLocal);
            float aa = fwidth(d);
            float alpha = 1.0 - smoothstep(1.0 - aa, 1.0, d);
            if (alpha <= 0.0) discard;
            FragColor = vec4(vColor.rgb, vColor.a * alpha);
        }
        """;

    private const int InitialCapacity = 16;

    private Shader? _shader;
    private UnitQuad? _quad;
    private uint _instanceBuffer;
    private int _capacity;
    private bool _disposed;

    public void Initialize()
    {
        GL gl = graphics.GL;
        _shader = new Shader(gl, VertexShader, FragmentShader);
        _quad = new UnitQuad(gl);

        _instanceBuffer = gl.GenBuffer();
        _capacity = InitialCapacity;

        gl.BindVertexArray(_quad.VertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceBuffer);
        Allocate(gl, _capacity);

        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, CircleInstance.SizeInBytes, CircleInstance.CenterOffset);
        gl.VertexAttribDivisor(1, 1);

        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, CircleInstance.SizeInBytes, CircleInstance.RadiusOffset);
        gl.VertexAttribDivisor(2, 1);

        gl.EnableVertexAttribArray(3);
        gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, CircleInstance.SizeInBytes, CircleInstance.ColorOffset);
        gl.VertexAttribDivisor(3, 1);

        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    public void Render(ReadOnlySpan<CircleInstance> instances, in Matrix4x4 viewProjection, Vector4 clearColor, Vector2 framebufferSize)
    {
        if (_shader is null || _quad is null) throw new InvalidOperationException("CircleRenderer is not initialized.");

        GL gl = graphics.GL;
        gl.Viewport(0, 0, (uint)framebufferSize.X, (uint)framebufferSize.Y);
        gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.Disable(EnableCap.DepthTest);

        if (instances.IsEmpty) return;

        EnsureCapacity(gl, instances.Length);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceBuffer);
        gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, instances);

        _shader.Use();
        Matrix4x4 matrix = viewProjection;
        gl.UniformMatrix4(_shader.UniformLocation("uViewProjection"), false, MemoryMarshal.CreateReadOnlySpan(ref matrix.M11, 16));

        gl.BindVertexArray(_quad.VertexArray);
        unsafe
        {
            gl.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0, (uint)instances.Length);
        }
        gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_instanceBuffer != 0)
        {
            graphics.GL.DeleteBuffer(_instanceBuffer);
            _instanceBuffer = 0;
        }
        _quad?.Dispose();
        _quad = null;
        _shader?.Dispose();
        _shader = null;
    }

    private void EnsureCapacity(GL gl, int count)
    {
        if (count <= _capacity) return;

        var capacity = _capacity;
        while (capacity < count) capacity *= 2;
        _capacity = capacity;

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceBuffer);
        Allocate(gl, capacity);
    }

    private static unsafe void Allocate(GL gl, int capacity) => gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(capacity * CircleInstance.SizeInBytes), (void*)0, BufferUsageARB.DynamicDraw);
}
