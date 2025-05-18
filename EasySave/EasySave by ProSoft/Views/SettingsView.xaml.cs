using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Globalization;
using EasySave_by_ProSoft.Properties;
using System.Diagnostics;
using EasySave_by_ProSoft.Localization;
using EasySave_by_ProSoft.ViewModels;

namespace EasySave_by_ProSoft.Views
{
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
        
        private void ValidateSettings_Click(object sender, RoutedEventArgs e)
        {
            // Update log format from ComboBox
            if (LogFormatComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _settingsViewModel.LogFormat = selectedItem.Content.ToString();
            }
            
            _settingsViewModel.SaveSettings();
            
            System.Windows.MessageBox.Show(
                Localization.Resources.SettingsSaved ?? "Settings saved successfully!",
                Localization.Resources.InformationTitle ?? "Information",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
