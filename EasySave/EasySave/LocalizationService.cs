using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace EasySave.Services
{
    /// <summary>
    /// Service de localisation pour charger et récupérer des chaînes localisées à partir de fichiers JSON.
    /// </summary>
    public class LocalizationService
    {
        private Dictionary<string, string> _localizedStrings;
        private string _currentLanguage;
        private bool _isLanguageLoaded;

        /// <summary>
        /// Obtient la langue actuellement chargée.
        /// </summary>
        public string CurrentLanguage => _currentLanguage;

        /// <summary>
        /// Indique si une langue a été chargée avec succès.
        /// </summary>
        public bool IsLanguageLoaded => _isLanguageLoaded;

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref="LocalizationService"/> avec une langue par défaut.
        /// </summary>
        /// <param name="defaultLanguage">Code de la langue par défaut (par exemple, "en").</param>
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

        /// <summary>
        /// Charge un fichier de langue JSON correspondant au code de langue spécifié.
        /// </summary>
        /// <param name="languageCode">Code de la langue à charger (par exemple, "fr").</param>
        /// <returns>True si la langue a été chargée avec succès, sinon False.</returns>
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

        /// <summary>
        /// Récupère une chaîne localisée correspondant à la clé spécifiée.
        /// </summary>
        /// <param name="key">Clé de la chaîne localisée.</param>
        /// <param name="args">Arguments optionnels pour formater la chaîne.</param>
        /// <returns>La chaîne localisée formatée ou un message d'erreur si la clé est introuvable ou mal formatée.</returns>
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