using EasySave_by_ProSoft.Services;
using EasySave_by_ProSoft.ViewModels;
using System.Windows;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// View for application settings - implements MVVM pattern
    /// </summary>
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        // Define constants for log formats
        private const string LOG_FORMAT_XML = "XML";
        private const string LOG_FORMAT_JSON = "JSON";

        private string _initialCultureName;
        private SettingsViewModel _settingsViewModel;
        private readonly IDialogService _dialogService;

        public SettingsView()
        {
            InitializeComponent();
            _dialogService = new DialogService(); // Initialize the dialog service
            _settingsViewModel = new SettingsViewModel(_dialogService);
            DataContext = _settingsViewModel;
            _initialCultureName = Thread.CurrentThread.CurrentUICulture.Name;

            // Set the checked status of radio buttons based on current language
            string currentLanguage = _settingsViewModel.UserLanguage;
            if (currentLanguage == "fr-FR")
            {
                FrenchRadioButton.IsChecked = true;
            }
            else
            {
                EnglishRadioButton.IsChecked = true;
            }

            // Initialize EncryptionKeyBox with current key value
            EncryptionKeyBox.Password = _settingsViewModel.EncryptionKey;

            // Subscribe to events
            _settingsViewModel.PropertyChanged += SettingsViewModel_PropertyChanged;


            // Initialize the log format ComboBox
            if (LogFormatComboBox != null)
            {
                if (_settingsViewModel.LogFormat.ToUpper() == LOG_FORMAT_XML)
                {
                    LogFormatComboBox.SelectedIndex = 1; // XML
                }
                else
                {
                    LogFormatComboBox.SelectedIndex = 0; // JSON by default
                }
            }
        }

        private void LanguageRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton radioButton && radioButton.IsChecked == true && radioButton.Tag != null)
            {
                string selectedCultureName = radioButton.Tag.ToString() ?? string.Empty;
                _settingsViewModel.LanguageChanged(selectedCultureName);
            }
        }
        // Event handler to synchronize PasswordBox with ViewModel
        private void EncryptionKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                if (EncryptionKeyBox.Password != vm.EncryptionKey)
                    vm.EncryptionKey = EncryptionKeyBox.Password;
            }
        }

        private void SettingsViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.EncryptionKey))
            {
                if (EncryptionKeyBox.Password != _settingsViewModel.EncryptionKey)
                    EncryptionKeyBox.Password = _settingsViewModel.EncryptionKey;
            }
        }
    }
}
