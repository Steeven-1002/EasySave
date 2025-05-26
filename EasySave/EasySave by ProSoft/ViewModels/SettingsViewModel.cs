using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Properties;
using EasySave_by_ProSoft.Services;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// ViewModel for the settings view implementing MVVM pattern
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly AppSettings _settings = AppSettings.Instance;
        private readonly IDialogService _dialogService;
        private string _selectedLogFormat;

        // Notification properties
        public event Action<string, string, bool> RequestApplicationRestartPrompt;
        public event Action LanguageChangeConfirmed;
        public event Action LanguageChangeCancelled;
        public event Action<Exception> ApplicationRestartFailed;

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
                        catch (ArgumentException ex) // Catch specific exception for invalid path characters
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to extract filename due to invalid characters: {ex.Message}");
                            // Optionally, notify the user or clear the input
                        }
                        catch (Exception ex) // General exception
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to extract filename: {ex.Message}");
                        }
                    }

                    // If user provided a process name with extension, ensure it's saved properly
                    // Allow process names without .exe as some system processes might not have it when queried.
                    // However, for user input, standardizing to include .exe if not present might be intended.
                    // The existing logic to add .exe if missing seems reasonable for user-defined business software.
                    if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(System.IO.Path.GetExtension(processName)))
                    {
                        // If it has an extension but it's not .exe, this might be an issue or intended.
                        // For now, keeping the logic that adds .exe if no extension or different.
                        // This part could be refined based on exact requirements for process name matching.
                    }
                    else if (string.IsNullOrEmpty(System.IO.Path.GetExtension(processName))) // Only add .exe if no extension is present
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
                var cleaned = Regex.Replace(value, @"(?<!^)\.", " .");

                var list = cleaned.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => s.Trim())
                                 .ToList();

                _settings.SetSetting("EncryptionExtensions", list);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public string ExtensionFilePriority
        {
            get
            {
                var setting = _settings.GetSetting("ExtensionFilePriority");
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
                // Ensure proper format for extension file priority
                var cleaned = Regex.Replace(value, @"(?<!^)\.", " .");

                var list = cleaned.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => s.Trim())
                                 .ToList();

                _settings.SetSetting("ExtensionFilePriority", list);
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

        public double LargeFileSizeThresholdKB
        {
            get
            {
                var setting = _settings.GetSetting("LargeFileSizeThresholdKey");
                if (setting is double d) return d;
                if (setting is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDouble(out double val)) return val;
                // Ensure "DefaultLargeFileSizeThresholdKey" is the correct key if it's meant to be a fallback from AppSettings
                // Or if it's a constant/default defined elsewhere.
                // Assuming "DefaultLargeFileSizeThresholdKey" is a key that might exist in settings.
                var defaultSetting = _settings.GetSetting("DefaultLargeFileSizeThresholdKey");
                if (defaultSetting is double defaultD) return defaultD;
                if (defaultSetting is JsonElement defaultJsonElement && defaultJsonElement.ValueKind == JsonValueKind.Number && defaultJsonElement.TryGetDouble(out double defaultVal)) return defaultVal;
                return 1000000; // Fallback value if no valid setting is found
            }
            set
            {
                if (LargeFileSizeThresholdKB != value)
                {
                    _settings.SetSetting("LargeFileSizeThresholdKey", value);
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        /// <summary>
        /// Command to save all settings
        /// </summary>
        public ICommand SaveSettingsCommand { get; }

        /// <summary>
        /// Command to validate and save all settings with user feedback
        /// </summary>
        public ICommand ValidateSettingsCommand { get; }

        public SettingsViewModel() : this(new DialogService()) { }

        public SettingsViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
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

            // Show confirmation message to the user using dialog service
            _dialogService.ShowInformation(
                Localization.Resources.SettingsSaved ?? "Settings saved successfully!",
                Localization.Resources.InformationTitle ?? "Information");
        }

        public void LanguageChanged(string newLanguage)
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

                // Trigger the restart prompt event
                RequestApplicationRestartPrompt?.Invoke(
                    Localization.Resources.LanguageChangeRestartMessage,
                    Localization.Resources.ConfirmationTitle,
                    true);
            }
            catch (CultureNotFoundException ex)
            {
                _dialogService.ShowError(
                    $"Culture {newLanguage} not found: {ex.Message}",
                    Localization.Resources.ErrorTitle);
                return;
            }
        }

        public void HandleApplicationRestartResult(bool restartConfirmed)
        {
            if (restartConfirmed)
            {
                LanguageChangeConfirmed?.Invoke();
            }
            else
            {
                // Revert to original language settings
                LanguageChangeCancelled?.Invoke();
            }
        }

        public void NotifyRestartFailed(Exception ex)
        {
            ApplicationRestartFailed?.Invoke(ex);
        }

        // Ensure all dialog interactions use the IDialogService and are invoked from the ViewModel.
        // Remove any direct UI logic or event handler dependencies.

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}