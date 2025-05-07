using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySave
{
    public class ConfigManager
    {
        private string _configFilePath;
        private Dictionary<string, object> _settings;
        private string _logFilePath;
        private string _stateFilePath;

        public ConfigManager(string configFilePath, string logFilePath, string stateFilePath)
        {
            _configFilePath = configFilePath;
            _logFilePath = logFilePath;
            _stateFilePath = stateFilePath;
            _settings = new Dictionary<string, object>();
        }

        public void LoadConfiguration()
        {
            // Charge la configuration depuis un fichier JSON
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }
                else
                {
                    // Définit les paramètres par défaut si le fichier n'existe pas
                    _settings["defaultLanguage"] = "en";
                    _settings["currentLanguage"] = "en";
                    _settings["logDirectory"] = @"C:\Logs\EasySave"; // Chemin par défaut (à adapter)
                    // ... autres paramètres par défaut
                    SaveConfiguration(); // Sauvegarde les paramètres par défaut
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement de la configuration : {ex.Message}");
                // Gère l'erreur (par exemple, utilise des valeurs par défaut, enregistre l'erreur)
            }
        }

        public void SaveConfiguration()
        {
            // Sauvegarde la configuration dans un fichier JSON
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde de la configuration : {ex.Message}");
                // Gère l'erreur
            }
        }

        public object GetSetting(string key)
        {
            // Récupère un paramètre de configuration
            _settings.TryGetValue(key, out object value);
            return value;
        }

        public void SetSetting(string key, object value)
        {
            // Définit un paramètre de configuration
            _settings[key] = value;
            SaveConfiguration(); // Sauvegarde immédiatement après avoir modifié un paramètre
        }

        // ... (Autres méthodes si nécessaire) ...
    }
}