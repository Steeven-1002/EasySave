using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Properties;
using EasySave_by_ProSoft.Services;
using EasySave_by_ProSoft.ViewModels;
using System.Diagnostics;
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
            _dialogService = new DialogService();
            _settingsViewModel = new SettingsViewModel(_dialogService);
            DataContext = _settingsViewModel;

            InitializeComponent();
            _initialCultureName = _settingsViewModel.UserLanguage;
            UpdateLanguageRadioButtons();

            // Hook up event handlers for the ViewModel's notification events
            _settingsViewModel.RequestApplicationRestartPrompt += OnRequestApplicationRestartPrompt;
            _settingsViewModel.LanguageChangeConfirmed += OnLanguageChangeConfirmed;
            _settingsViewModel.LanguageChangeCancelled += OnLanguageChangeCancelled;
            _settingsViewModel.ApplicationRestartFailed += OnApplicationRestartFailed;

            // Load the encryption key if it exists
            string? savedKey = AppSettings.Instance.GetSetting("EncryptionKey") as string;
            if (!string.IsNullOrEmpty(savedKey))
            {
                EncryptionKeyBox.Password = savedKey;
            }

            // Synchronize PasswordBox with ViewModel if EncryptionKey changes in ViewModel
            _settingsViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.EncryptionKey))
                {
                    if (EncryptionKeyBox.Password != _settingsViewModel.EncryptionKey)
                        EncryptionKeyBox.Password = _settingsViewModel.EncryptionKey;
                }
            };

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

        private void UpdateLanguageRadioButtons()
        {
            if (FrenchRadioButton != null) FrenchRadioButton.IsChecked = false;
            if (EnglishRadioButton != null) EnglishRadioButton.IsChecked = false;

            if (_initialCultureName.StartsWith("fr") && FrenchRadioButton != null)
            {
                FrenchRadioButton.IsChecked = true;
            }
            else if (_initialCultureName.StartsWith("en") && EnglishRadioButton != null)
            {
                EnglishRadioButton.IsChecked = true;
            }
            else // Default language if current culture is neither "en" nor "fr"
            {
                if (EnglishRadioButton != null) EnglishRadioButton.IsChecked = true;
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

        // Event handler for application restart prompt request
        private void OnRequestApplicationRestartPrompt(string message, string title, bool isQuestion)
        {
            System.Windows.Forms.MessageBox.Show(
            EasySave_by_ProSoft.Localization.Resources.LanguageChangeRestartMessage,
            "Restart",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Information
            );
            _settingsViewModel.HandleApplicationRestartResult(true);
        }

        // Event handler for application restart confirmation
        private void OnLanguageChangeConfirmed()
        {
            try
            {
                string applicationPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(applicationPath))
                {
                    applicationPath = System.Windows.Application.ResourceAssembly.Location;
                }
                Process.Start(applicationPath);
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _settingsViewModel.NotifyRestartFailed(ex);
            }
        }

        // Event handler for application restart cancellation
        private void OnLanguageChangeCancelled()
        {
            _dialogService.ShowInformation(
                Localization.Resources.LanguageChangeCancelledMessage,
                Localization.Resources.InformationTitle);

            Settings.Default.UserLanguage = _initialCultureName;
            Settings.Default.Save();
        }

        // Event handler for application restart failure
        private void OnApplicationRestartFailed(Exception ex)
        {
            _dialogService.ShowError(
                $"{Localization.Resources.ErrorRestartingApplicationMessage}\n{ex.Message}",
                Localization.Resources.ErrorTitle);

            Settings.Default.UserLanguage = _initialCultureName;
            Settings.Default.Save();
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
    }
}
