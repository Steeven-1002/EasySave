using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySave.Services
{
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private Dictionary<string, object> _settings; // 'object' comme dans le diagramme

        public string LogFilePath => GetSetting("LogFilePath") as string ?? "Logs"; // Valeur par défaut
        public string StateFilePath => GetSetting("StateFilePath") as string ?? "state.json"; // Valeur par défaut
        public string Language => GetSetting("Language") as string ?? "en"; // Valeur par défaut

        public ConfigManager(string configFilePath)
        {
            _configFilePath = configFilePath;
            _settings = new Dictionary<string, object>();
            LoadConfiguration();
        }

        public ConfigManager()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave", "app_settings.json"))
        {
        }

        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                    Console.WriteLine($"ConfigManager: Configuration loaded from '{_configFilePath}'.");
                }
                else
                {
                    Console.WriteLine($"ConfigManager: File '{_configFilePath}' not found. Using/creating default settings.");
                    // Appliquer et sauvegarder les valeurs par défaut
                    SetSetting("LogFilePath", "Logs"); // Par défaut, un dossier "Logs"
                    SetSetting("StateFilePath", "state.json");
                    SetSetting("Language", "en");
                    SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfigManager: Error loading configuration: {ex.Message}. Using empty settings.");
                _settings = new Dictionary<string, object>();
            }
        }

        public void SaveConfiguration()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
                Console.WriteLine($"ConfigManager: Configuration saved to '{_configFilePath}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfigManager: Error saving configuration: {ex.Message}");
            }
        }

        public object? GetSetting(string key)
        {
            return _settings.TryGetValue(key, out var value) ? value : null;
        }

        public void SetSetting(string key, object value)
        {
            _settings[key] = value;
            // Pourrait appeler SaveConfiguration() ici si les changements doivent être immédiats,
            // ou laisser l'appelant décider quand sauvegarder.
        }
    }
}