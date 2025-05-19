using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Globalization;
using EasySave_by_ProSoft.Properties;
using System.Diagnostics;
using EasySave_by_ProSoft.Localization;
using EasySave_by_ProSoft.ViewModels;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// View for application settings - implements MVVM pattern
    /// </summary>
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private string _initialCultureName;
        private SettingsViewModel _settingsViewModel;

        public SettingsView()
        {
            _settingsViewModel = new SettingsViewModel();
            DataContext = _settingsViewModel;

            InitializeComponent();
            _initialCultureName = _settingsViewModel.UserLanguage;
            UpdateLanguageRadioButtons();

            // Recharge la clé de chiffrement si elle existe
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
                if (_settingsViewModel.LogFormat.ToUpper() == "XML")
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
                _settingsViewModel.LanguageChanged(selectedCultureName, this);
            }
        }

        public void PromptForApplicationRestart()
        {
            System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
                Localization.Resources.LanguageChangeRestartMessage,
                Localization.Resources.ConfirmationTitle,
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    string applicationPath = Process.GetCurrentProcess().MainModule.FileName;
                    if (string.IsNullOrEmpty(applicationPath))
                    {
                        applicationPath = System.Windows.Application.ResourceAssembly.Location;
                    }
                    Process.Start(applicationPath);
                    System.Windows.Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"{Localization.Resources.ErrorRestartingApplicationMessage}\n{ex.Message}",
                        Localization.Resources.ErrorTitle,
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    Settings.Default.UserLanguage = _initialCultureName;
                    Settings.Default.Save();
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    Localization.Resources.LanguageChangeCancelledMessage,
                    Localization.Resources.InformationTitle,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                Settings.Default.UserLanguage = _initialCultureName;
                Settings.Default.Save();
            }
        }

        // Add this event handler to synchronize PasswordBox with ViewModel
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
