using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Properties;
using EasySave_by_ProSoft.Views;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// ViewModel for the settings view implementing MVVM pattern
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly AppSettings _settings = AppSettings.Instance;
        private string _selectedLogFormat;

        public string BusinessSoftwareName
        {
            get => _settings.GetSetting("BusinessSoftwareName")?.ToString() ?? string.Empty;
            set
            {
                // Ensure proper process name validation
                string processName = value;

                // Remove any unwanted characters that could cause process detection issues
                if (!string.IsNullOrEmpty(processName))
                {
                    // If user provided a full path, extract just the filename
                    if (processName.Contains("\\") || processName.Contains("/"))
                    {
                        try
                        {
                            processName = System.IO.Path.GetFileName(processName);
                            System.Diagnostics.Debug.WriteLine($"Extracted filename from path: {processName}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to extract filename: {ex.Message}");
                        }
                    }

                    // If user provided a process name with extension, ensure it's saved properly
                    if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        processName = $"{processName}.exe";
                        System.Diagnostics.Debug.WriteLine($"Added .exe extension to process name: {processName}");
                    }
                }

                _settings.SetSetting("BusinessSoftwareName", processName);
                OnPropertyChanged();
                SaveSettings();

                // Verify the process name can be correctly extracted
                string extractedName = System.IO.Path.GetFileNameWithoutExtension(processName);
                System.Diagnostics.Debug.WriteLine($"Process name that will be used for detection: {extractedName}");
            }
        }

        public string EncryptionExtensions
        {
            get
            {
                var setting = _settings.GetSetting("EncryptionExtensions");
                if (setting is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        list.Add(item.GetString() ?? "");
                    }
                    return string.Join(", ", list);
                }
                return string.Empty;
            }
            set
            {
                // Ajoute un espace avant chaque '.' sauf le premier caract�re (pour g�rer les cas concat�n�s)
                var cleaned = System.Text.RegularExpressions.Regex.Replace(value, @"(?<!^)\.", " .");

                var list = cleaned.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => s.Trim())
                                 .ToList();

                _settings.SetSetting("EncryptionExtensions", list);
                OnPropertyChanged();
                SaveSettings();
            }

        }

        public string EncryptionKey
        {
            get => _settings.GetSetting("EncryptionKey")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("EncryptionKey", value); OnPropertyChanged(); SaveSettings(); }
        }

        public string LogFormat
        {
            get => _settings.GetSetting("LogFormat")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("LogFormat", value); OnPropertyChanged(); SaveSettings(); LoggingService.RecreateInstance(value); }
        }

        public string SelectedLogFormat
        {
            get => _selectedLogFormat;
            set
            {
                _selectedLogFormat = value;
                LogFormat = value;
                OnPropertyChanged();
            }
        }

        public string UserLanguage
        {
            get => _settings.GetSetting("UserLanguage")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("UserLanguage", value); OnPropertyChanged(); }
        }

        /// <summary>
        /// Command to save all settings
        /// </summary>
        public ICommand SaveSettingsCommand { get; }

        /// <summary>
        /// Command to validate and save all settings with user feedback
        /// </summary>
        public ICommand ValidateSettingsCommand { get; }

        public SettingsViewModel()
        {
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => true);
            ValidateSettingsCommand = new RelayCommand(_ => ValidateSettings(), _ => true);

            // Initialize selected log format
            _selectedLogFormat = LogFormat;
        }

        /// <summary>
        /// Saves settings to configuration file
        /// </summary>
        public void SaveSettings()
        {
            _settings.SaveConfiguration();
        }

        /// <summary>
        /// Validates and saves settings with user feedback
        /// </summary>
        public void ValidateSettings()
        {
            // Save settings to configuration file
            SaveSettings();

            // Show confirmation message to the user
            System.Windows.MessageBox.Show(
                Localization.Resources.SettingsSaved ?? "Settings saved successfully!",
                Localization.Resources.InformationTitle ?? "Information",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void LanguageChanged(string newLanguage, SettingsView settingsViewInstance)
        {
            if (newLanguage == UserLanguage)
            {
                return; // No change in language
            }
            UserLanguage = newLanguage;
            SaveSettings();

            try
            {
                Settings.Default.UserLanguage = newLanguage;
                Settings.Default.Save();
                CultureInfo newCulture = new CultureInfo(newLanguage);
                Thread.CurrentThread.CurrentUICulture = newCulture;
                Thread.CurrentThread.CurrentCulture = newCulture;
                if (Localization.Resources.Culture != null || Localization.Resources.Culture == null)
                {
                    Localization.Resources.Culture = newCulture;
                }

                settingsViewInstance.PromptForApplicationRestart();
            }
            catch (CultureNotFoundException ex)
            {
                System.Windows.MessageBox.Show($"Culture {newLanguage} non trouv�e: {ex.Message}", Localization.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}