using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Properties;
using EasySave_by_ProSoft.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace EasySave_by_ProSoft.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly AppSettings _settings = AppSettings.Instance;

        public string BusinessSoftwareName
        {
            get => _settings.GetSetting("BusinessSoftwareName")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("BusinessSoftwareName", value); OnPropertyChanged(); }
        }

        public ObservableCollection<string> EncryptionExtensions
        {
            get => new(_settings.GetSetting("EncryptionExtensions")?.ToString().Split(',') ?? Array.Empty<string>());
            set { _settings.SetSetting("EncryptionExtensions", value); OnPropertyChanged(); }
        }

        public String LogFormat
        {
            get => _settings.GetSetting("LogFormat")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("LogFormat", value); OnPropertyChanged(); }
        }

        public string UserLanguage
        {
            get => _settings.GetSetting("UserLanguage")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("UserLanguage", value); OnPropertyChanged(); }
        }

        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel()
        {
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => true);
        }

        public void SaveSettings()
        {
            _settings.SaveConfiguration();
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

        internal static void LoadSettings()
        {
            
        }
    }
}