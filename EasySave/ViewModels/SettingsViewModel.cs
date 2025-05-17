using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly Settings _settings = Settings.Instance;

        public string BusinessSoftwareName
        {
            get => _settings.BusinessSoftwareName;
            set { _settings.BusinessSoftwareName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> EncryptionExtensions
        {
            get => new(_settings.EncryptionExtensions ?? new());
            set { _settings.EncryptionExtensions = new List<string>(value); OnPropertyChanged(); }
        }

        public LogFormat LogFormat
        {
            get => _settings.LogFormat;
            set { _settings.ChangeLogFormat(value); OnPropertyChanged(); }
        }

        public string UserLanguage
        {
            get => _settings.UserLanguage;
            set { _settings.UserLanguage = value; OnPropertyChanged(); }
        }

        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel()
        {
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => true);
        }

        private void SaveSettings()
        {
            _settings.Save();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}