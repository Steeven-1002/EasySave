using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Properties;
using EasySave_by_ProSoft.Views;
using System.Collections.ObjectModel;
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
            set { _settings.SetSetting("BusinessSoftwareName", value); OnPropertyChanged(); SaveSettings(); }
        }

        public string EncryptionExtensions
        {
            get
            {
               var setting = _settings.GetSetting("EncryptionExtensions");
                return string.Join(", ", setting.ToString());
            }
            set
            {
                _settings.SetSetting("EncryptionExtensions", value);
                OnPropertyChanged();
                SaveSettings();
            }
        }

        public string LogFormat
        {
            get => _settings.GetSetting("LogFormat")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("LogFormat", value); OnPropertyChanged(); SaveSettings(); }
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
                System.Windows.MessageBox.Show($"Culture {newLanguage} non trouvée: {ex.Message}", Localization.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}