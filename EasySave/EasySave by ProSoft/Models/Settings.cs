using System;
using System.IO;
using System.Text.Json;

namespace EasySave_by_ProSoft.Models {
    public class Settings {
        private static Settings? instance;
        private static readonly string settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "EasySave", 
            "settings.json"
        );

        public LogFormat LogFormat { get; set; } = LogFormat.JSON;
        public string BusinessSoftwareName { get; set; } = "";
        public List<string> EncryptionExtensions { get; set; } = new List<string>();
        
        private Settings() {
            // Private constructor to enforce singleton
        }
        
        public static Settings Instance {
            get {
                if (instance == null) {
                    instance = Load();
                }
                return instance;
            }
        }
        
        public static Settings Load() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath));
                
                if (File.Exists(settingsFilePath)) {
                    string json = File.ReadAllText(settingsFilePath);
                    var loadedSettings = JsonSerializer.Deserialize<Settings>(json);
                    if (loadedSettings != null) {
                        return loadedSettings;
                    }
                }
            }
            catch (Exception ex) {
                // Log error
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
            
            return new Settings();
        }
        
        public void Save() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath));
                
                var options = new JsonSerializerOptions {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex) {
                // Log error
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
        
        public void ChangeLogFormat(LogFormat newFormat) {
            if (LogFormat != newFormat) {
                LogFormat = newFormat;
                Save();
            }
        }
    }
}