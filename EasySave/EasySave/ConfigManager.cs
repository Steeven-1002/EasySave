using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySave.Services
{
    /// <summary>
    /// Manages the application's configuration settings, including loading and saving them to a file.
    /// Implements the Singleton design pattern.
    /// </summary>
    public class ConfigManager
    {
        private static ConfigManager? _instance;
        private readonly string _configFilePath;
        private Dictionary<string, object> _settings;

        /// <summary>
        /// Gets the singleton instance of the <see cref="ConfigManager"/> class.
        /// </summary>
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

        /// <summary>
        /// Gets the file path for the log file from the configuration.
        /// Defaults to "log.txt" if not specified.
        /// </summary>
        public string LogFilePath => GetSetting("LogFilePath")?.ToString() ?? "log.txt";

        /// <summary>
        /// Gets the file path for the state file from the configuration.
        /// Defaults to "state.json" if not specified.
        /// </summary>
        public string StateFilePath => GetSetting("StateFilePath")?.ToString() ?? "state.json";

        /// <summary>
        /// Gets the language setting from the configuration.
        /// Defaults to "en" if not specified.
        /// </summary>
        public string Language => GetSetting("Language")?.ToString() ?? "en";

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigManager"/> class.
        /// Loads the configuration from the specified file path.
        /// </summary>
        /// <param name="configFilePath">The path to the configuration file.</param>
        private ConfigManager(string configFilePath)
        {
            _configFilePath = configFilePath;
            _settings = new Dictionary<string, object>();
            LoadConfiguration();
        }

        /// <summary>
        /// Loads the configuration settings from the file.
        /// If the file does not exist, an empty configuration is used.
        /// </summary>
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

        /// <summary>
        /// Saves the current configuration settings to the file.
        /// </summary>
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

        /// <summary>
        /// Gets the value of a configuration setting by its key.
        /// </summary>
        /// <param name="key">The key of the setting to retrieve.</param>
        /// <returns>The value of the setting, or <c>null</c> if the key does not exist.</returns>
        public object? GetSetting(string key)
        {
            _settings.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>
        /// Sets the value of a configuration setting.
        /// If the key already exists, its value is updated.
        /// </summary>
        /// <param name="key">The key of the setting to set.</param>
        /// <param name="value">The value to assign to the setting.</param>
        public void SetSetting(string key, object value)
        {
            _settings[key] = value;
        }
    }
}