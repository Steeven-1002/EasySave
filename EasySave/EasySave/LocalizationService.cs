using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace EasySave.Services
{
    public class LocalizationService
    {
        private Dictionary<string, string> _localizedStrings;
        private string _currentLanguage;
        private bool _isLanguageLoaded;

        public string CurrentLanguage => _currentLanguage;
        public bool IsLanguageLoaded => _isLanguageLoaded;

        public LocalizationService(string defaultLanguage = "en")
        {
            _localizedStrings = new Dictionary<string, string>();
            _currentLanguage = defaultLanguage;
            _isLanguageLoaded = LoadLanguage(_currentLanguage);

            if (!_isLanguageLoaded)
            {
                Console.WriteLine($"LocalizationService WARNING: Default language '{defaultLanguage}' could not be loaded. Check Resources folder and file content.");
            }
        }

        public bool LoadLanguage(string languageCode)
        {
            string fileName = $"lang_{languageCode}.json";
            _isLanguageLoaded = false;

            try
            {
                string? exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string filePath = Path.Combine(exePath ?? ".", "Resources", fileName);

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"LocalizationService Error: Language file '{filePath}' not found.");
                    return false;
                }

                string jsonContent = File.ReadAllText(filePath);
                var newStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                if (newStrings != null)
                {
                    _localizedStrings = newStrings;
                    _currentLanguage = languageCode;
                    _isLanguageLoaded = true;
                    Console.WriteLine($"LocalizationService: Language '{languageCode}' loaded successfully.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"LocalizationService Error: Could not parse JSON in language file '{filePath}'. It might be empty or malformed.");
                    return false;
                }
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"LocalizationService Error parsing JSON in '{fileName}': {jsonEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LocalizationService Error loading language file '{fileName}': {ex.Message}");
                return false;
            }
        }

        public string GetString(string key, params object[] args)
        {
            if (!_isLanguageLoaded)
            {
                return $"[No Lang Loaded! Key: {key}]";
            }

            if (_localizedStrings.TryGetValue(key, out string? formatString) && formatString != null)
            {
                try
                {
                    return (args != null && args.Length > 0) ? string.Format(formatString, args) : formatString;
                }
                catch (FormatException ex)
                {
                    return $"[FORMAT ERROR for key '{key}' in lang '{_currentLanguage}': {ex.Message} | Original: '{formatString}']";
                }
            }
            return $"[MISSING KEY: '{key}' in lang '{_currentLanguage}']";
        }
    }
}