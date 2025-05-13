using System.Reflection;
using System.Text.Json;

namespace EasySave.Services
{
    /// <summary>
    /// Language localization service for loading and retrieving localized strings.
    /// </summary>
    public class LocalizationService
    {
        private Dictionary<string, string> _localizedStrings;
        private string _currentLanguage;
        private bool _isLanguageLoaded;
            
        /// <summary>
        /// Gets the currently loaded language.
        /// </summary>
        public string CurrentLanguage => _currentLanguage;

        /// <summary>
        /// Indicates whether a language has been successfully loaded.
        /// </summary>
        public bool IsLanguageLoaded => _isLanguageLoaded;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalizationService"/> class with a default language.
        /// </summary>
        /// <param name="defaultLanguage">Default language code (e.g., "en").</param>
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
        /// Loads a JSON language file corresponding to the specified language code.
        /// </summary>
        /// <param name="languageCode">Language code to load (e.g., "fr").</param>
        /// <returns>True if the language file was loaded successfully; otherwise, false.</returns>
        public bool LoadLanguage(string languageCode)
        {
            string fileName = $"lang_{languageCode}.json";
            _isLanguageLoaded = false;

            try
            {
                string? exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filePath = Path.Combine(exePath ?? "ASSETS", "lang", fileName);

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
        /// Retrieves a localized string corresponding to the specified key.
        /// <param name="key">Key of the localized string.</param>
        /// <param name="args">Optional arguments to format the string.</param>
        /// <returns>The formatted localized string or an error message if the key is not found or is misformatted.</returns>
        /// </summary>
        public string GetString(string key, params object[]? args)
        {
            if (!_isLanguageLoaded)
            {
                return $"[No Lang Loaded! Key: {key}]";
            }

            if (_localizedStrings.TryGetValue(key, out string? formatString))
            {
                try
                {
                    return args is { Length: > 0 } ? string.Format(formatString, args) : formatString;
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