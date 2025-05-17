using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;

namespace EasySave_by_ProSoft.Models
{
    public class AppSettings
    {
        private static AppSettings? instance;
        private string? configFilePath =
          Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EasySave",
                "config.json"
            );
        private Dictionary<string, JsonElement> settings = new Dictionary<string, JsonElement>();
        public static AppSettings Instance
        {
            get
            {
                instance ??= new AppSettings();
                return instance;
            }
        }

        public void LoadConfiguration()
        {
            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new Dictionary<string, JsonElement>();
            }
            else
            {
                // Create default settings if the config file does not exist
                settings["LogFormat"] = JsonDocument.Parse("\"XML\"").RootElement;
                settings["UserLanguage"] = JsonDocument.Parse("\"en-US\"").RootElement;
                SaveConfiguration();
            }
        }

        public void SaveConfiguration()
        {
            string json = JsonSerializer.Serialize(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(configFilePath)!);
            File.WriteAllText(configFilePath, json);
        }

        public object? GetSetting(string key)
        {
            if (settings.TryGetValue(key, out JsonElement value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Array => value.EnumerateArray(),
                    _ => null
                };
            }
            return null;
        }

        public void SetSetting(string key, object value)
        {
            JsonElement jsonValue = value switch
            {
                string str => JsonDocument.Parse($"\"{str}\"").RootElement,
                int num => JsonDocument.Parse(num.ToString()).RootElement,
                double dbl => JsonDocument.Parse(dbl.ToString()).RootElement,
                bool b => JsonDocument.Parse(b.ToString().ToLower()).RootElement,
                _ => throw new ArgumentException("Unsupported value type")
            };

            settings[key] = jsonValue;
        }
    }
}
