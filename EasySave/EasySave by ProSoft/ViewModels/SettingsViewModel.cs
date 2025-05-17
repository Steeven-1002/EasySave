using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EasySave_by_ProSoft.Models;

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
            get => _settings.GetSetting("BusinessSoftwareName")?.ToString() ?? string.Empty;
            set { _settings.SetSetting("UserLanguage", value); OnPropertyChanged(); }
        }

        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel()
        {
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => true);
        }

        private void SaveSettings()
        {
            _settings.SaveConfiguration();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}