using System.IO;
using System.Text.Json;

namespace EasySave_by_ProSoft.Models
{
    public class AppSettings
    {
        private static AppSettings? instance;
        private string? configFilePath =
          Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                "config.json"
            );
        private Dictionary<string, JsonElement> settings = new Dictionary<string, JsonElement>();
        private AppSettings()
        {
            LoadConfiguration();
        }
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
                settings["BusinessSoftwareName"] = JsonDocument.Parse("\"calc.exe\"").RootElement;
                settings["EncryptionExtensions"] = JsonDocument.Parse("[\".txt\", \".docx\"]").RootElement;
                settings["EncryptionKey"] = JsonDocument.Parse("\"defaultKey\"").RootElement;
                settings["LogFormat"] = JsonDocument.Parse("\"XML\"").RootElement;
                settings["UserLanguage"] = JsonDocument.Parse("\"en-US\"").RootElement;
                settings["LargeFileSizeThresholdKey"] = JsonDocument.Parse("1000000").RootElement;
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
                    JsonValueKind.Array => value,
                    _ => null
                };
            }
            return null;
        }

        public void SetSetting(string key, object value)
        {
            JsonElement jsonValue;

            if (value is string str)
            {
                jsonValue = JsonDocument.Parse($"\"{str}\"").RootElement;
            }
            else if (value is int or double or bool)
            {
                string jsonString = JsonSerializer.Serialize(value);
                jsonValue = JsonDocument.Parse(jsonString).RootElement;
            }
            else if (value is IEnumerable<string> stringList)
            {
                string jsonList = JsonSerializer.Serialize(stringList);
                jsonValue = JsonDocument.Parse(jsonList).RootElement;
            }
            else
            {
                throw new ArgumentException("Unsupported value type");
            }

            settings[key] = jsonValue;
        }
    }
}
