using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySave.Services
{
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private Dictionary<string, JsonElement> _settings;

        public string LogFilePath =>
            _settings.TryGetValue("LogFilePath", out var val) && val.ValueKind == JsonValueKind.String
                ? val.GetString()!
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave", "Logs\\");

        public string StateFilePath =>
            _settings.TryGetValue("StateFilePath", out var val) && val.ValueKind == JsonValueKind.String
                ? val.GetString()!
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave", "State\\");

        public string Language =>
            _settings.TryGetValue("Language", out var val) && val.ValueKind == JsonValueKind.String
                ? val.GetString()!
                : "en";

        public ConfigManager(string configFilePath)
        {
            _configFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                configFilePath
            );
            _settings = new Dictionary<string, JsonElement>();
            LoadConfiguration();
        }


        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    // The configuration file exists
                    // We load it, deserialize it and store it in the _settings dictionary
                    string json = File.ReadAllText(_configFilePath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new Dictionary<string, JsonElement>();
                }
                else
                {
                    // No file have been found, create a new one with default values
                    // The default values are set in the attributes
                    // Apply and save default values
                    SetSetting("LogFilePath", LogFilePath);
                    SetSetting("StateFilePath", StateFilePath);
                    SetSetting("Language", Language);
                    SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfigManager: Error loading configuration: {ex.Message}. Using empty settings.");
                _settings = new Dictionary<string, JsonElement>();
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                // The configuration live inside a directory
                // We need to check if the directory exists
                // If not, we create it
                string directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory) && directory != null)
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
                Console.WriteLine($"ConfigManager: Configuration saved to '{_configFilePath}'.");
            }
            catch (Exception error)
            {
                Console.WriteLine($"ConfigManager: Error saving configuration: {error.Message}");
            }
        }

        public object? GetSetting(string key)
        {
            return _settings.TryGetValue(key, out var value) ? value : null;
        }

        public void SetSetting(string key, object value)
        {
            if (value is JsonElement jsonElement)
            {
                _settings[key] = jsonElement;
            }
            else
            {
                string json = JsonSerializer.Serialize(value);
                _settings[key] = JsonSerializer.Deserialize<JsonElement>(json);
            }
        }
    }
}