using System.Diagnostics;

namespace XEngine.Core.Graphics.OpenGL
{
    public sealed class GLProvider(string shaderFolder) : IDisposable
    {
        private readonly Dictionary<string, Shader> _shaders = [];
        private bool _disposed = false;

        public readonly string ShaderFolder = shaderFolder;
        public UnitQuad UnitQuad { get; private set; } = new UnitQuad();

        public void Init()
        {
            UnitQuad.Init();
            InitDefaultSpriteShader();
            InitErrorShader();
        }

        public Shader GetShader(string name)
        {
            if (_shaders.TryGetValue(name, out var shader)) return shader;

            Debug.WriteLine($"[Error] Shader '{name}' not found. Falling back to ErrorShader.");
            return _shaders.GetValueOrDefault("Error")
                   ?? throw new Exception("[Fatal] ErrorShader is missing.");
        }

        public Shader LoadShader(string path, string name)
        {
            if (_shaders.TryGetValue(name, out Shader? value)) return value;
            string _shader_path = Path.Combine(ShaderFolder, path);
            return _shaders[name] = Shader.FromFolder(_shader_path);
        }

        private void InitErrorShader()
        {
            var _shaderPath = Path.Combine(ShaderFolder, "Error");
            _shaders["Error"] = Shader.FromFolder(_shaderPath);
        }

        private void InitDefaultSpriteShader()
        {
            var _shaderPath = Path.Combine(ShaderFolder, "Default");
            _shaders["Sprite"] = Shader.FromFolder(_shaderPath);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var _s in _shaders.Values) _s.Dispose();
            _shaders.Clear();
        }
    }
}
