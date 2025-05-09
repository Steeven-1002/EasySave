using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySave.Services
{
    public class ConfigManager
    {
        private static ConfigManager? _instance;
        private readonly string _configFilePath;
        private Dictionary<string, object> _settings;

        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ConfigManager("app_settings.json");
                }
                return _instance;
            }
        }
        public string LogFilePath => GetSetting("LogFilePath")?.ToString() ?? "log.txt";
        public string StateFilePath => GetSetting("StateFilePath")?.ToString() ?? "state.json";
        public string Language => GetSetting("Language")?.ToString() ?? "en";

        private ConfigManager(string configFilePath)
        {
            _configFilePath = configFilePath;
            _settings = new Dictionary<string, object>();
            LoadConfiguration();
        }

        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfigManager ERROR loading configuration: {ex.Message}");
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfigManager ERROR saving configuration: {ex.Message}");
            }
        }

        public object? GetSetting(string key)
        {
            _settings.TryGetValue(key, out var value);
            return value;
        }

        public void SetSetting(string key, object value)
        {
            _settings[key] = value;
        }
    }
}