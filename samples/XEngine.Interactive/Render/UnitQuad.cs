using Silk.NET.OpenGL;

namespace XEngine.Interactive.Render;

public sealed class UnitQuad : IDisposable
{
    private static readonly float[] Vertices =
    [
        -1f, -1f,
         1f, -1f,
         1f,  1f,
        -1f,  1f,
    ];

    private static readonly uint[] Indices = [0, 1, 2, 2, 3, 0];

    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private bool _disposed;

    public UnitQuad(GL gl)
    {
        _gl = gl;
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, Vertices.AsSpan(), BufferUsageARB.StaticDraw);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), IntPtr.Zero);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, Indices.AsSpan(), BufferUsageARB.StaticDraw);

        _gl.BindVertexArray(0);
    }

    public uint VertexArray => _vao;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
    }
}
