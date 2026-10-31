using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Text.Json.Serialization;
using XEngine.Core.Input.InputAxis;

namespace XEngine.Core.Config
{
                public class GameConfig
    {
                                [JsonPropertyName("debug")]
        public bool Debug { get; set; } = true;

                                [JsonPropertyName("keymap")]
        public Dictionary<string, Keys> KeyMap { get; set; } = [];

                                [JsonPropertyName("axes")]
        public Dictionary<string, AxisSettings> Axes { get; set; } = [];
    }
}
