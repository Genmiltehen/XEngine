using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XEngine.Core.Config
{
                public class ConfigManager
    {
        private readonly JsonSerializerOptions _option = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

                                                public void Save(string path, GameConfig config)
        {
            string json = JsonSerializer.Serialize(config, _option);
            File.WriteAllText(path, json);
        }

                                                public GameConfig Load(string path)
        {
            if (!File.Exists(path)) return new GameConfig();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameConfig>(json, _option) ?? new GameConfig();
        }
    }
}
