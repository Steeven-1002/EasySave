using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySave
{
    public class LocalizationService
    {
        private Dictionary<string, string> _localizedStrings;
        private string _currentLanguage;
        private bool _isLanguageLoaded;
        private string _defaultLanguage;

        public LocalizationService(string defaultLanguage = "en")
        {
            _defaultLanguage = defaultLanguage;
            _currentLanguage = defaultLanguage;
            _localizedStrings = new Dictionary<string, string>();
            _isLanguageLoaded = false;
        }

        public bool LoadLanguage(string languageCode)
        {
            // Charge les chaînes de caractères localisées pour une langue donnée
            try
            {
                string languageFilePath = < span class="math-inline">"lang\_\{languageCode\}\.json"; // Chemin relatif ou absolu
if \(File\.Exists\(languageFilePath\)\)
\{
string json \= File\.ReadAllText\(languageFilePath\);
\_localizedStrings \= JsonSerializer\.Deserialize<Dictionary<string, string\>\>\(json\);
\_currentLanguage \= languageCode;
\_isLanguageLoaded \= true;
return true;
\}
else
\{
Console\.WriteLine\(</span>"Fichier de langue non trouvé : {languageFilePath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement de la langue : {ex.Message}");
return false;
            }
        }

        public string GetString(string key, params object[] args)
{
    // Récupère une chaîne de caractères localisée
    if (_isLanguageLoaded && _localizedStrings.TryGetValue(key, out string value))
    {
        return string.Format(value, args);
    }
            // Si la clé n'est pas trouvée ou la langue n'est pas chargée, retourne la clé elle-même