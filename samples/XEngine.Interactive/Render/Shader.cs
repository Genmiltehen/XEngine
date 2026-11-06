using Silk.NET.OpenGL;

namespace XEngine.Interactive.Render;

public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniforms = [];
    private readonly uint _program;
    private bool _disposed;

    public Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        var vertex = Compile(ShaderType.VertexShader, vertexSource);
        var fragment = Compile(ShaderType.FragmentShader, fragmentSource);

        _program = _gl.CreateProgram();
        _gl.AttachShader(_program, vertex);
        _gl.AttachShader(_program, fragment);
        _gl.LinkProgram(_program);

        _gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out var status);
        if (status == 0)
        {
            var log = _gl.GetProgramInfoLog(_program);
            _gl.DeleteProgram(_program);
            _gl.DeleteShader(vertex);
            _gl.DeleteShader(fragment);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        _gl.DetachShader(_program, vertex);
        _gl.DetachShader(_program, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
    }

    public void Use() => _gl.UseProgram(_program);

    public int UniformLocation(string name)
    {
        if (_uniforms.TryGetValue(name, out var location)) return location;
        location = _gl.GetUniformLocation(_program, name);
        _uniforms.Add(name, location);
        return location;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gl.DeleteProgram(_program);
    }

    private uint Compile(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status != 0) return shader;

        var log = _gl.GetShaderInfoLog(shader);
        _gl.DeleteShader(shader);
        throw new InvalidOperationException($"{type} compilation failed: {log}");
    }
}
