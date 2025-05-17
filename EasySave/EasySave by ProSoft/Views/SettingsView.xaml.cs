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
        private SettingsViewModel _settingsViewModel = new SettingsViewModel();

        public SettingsView()
        {
            InitializeComponent();
            _initialCultureName = Thread.CurrentThread.CurrentUICulture.Name;
            UpdateLanguageRadioButtons();
        }

        private void UpdateLanguageRadioButtons()
        {
            string currentCultureUI = Thread.CurrentThread.CurrentUICulture.Name;

            if (FrenchRadioButton != null) FrenchRadioButton.IsChecked = false;
            if (EnglishRadioButton != null) EnglishRadioButton.IsChecked = false;

            if (currentCultureUI.StartsWith("fr") && FrenchRadioButton != null)
            {
                FrenchRadioButton.IsChecked = true;
            }
            else if (currentCultureUI.StartsWith("en") && EnglishRadioButton != null)
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
                    RestoreInitialCultureAndRadioButtonState();
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
                RestoreInitialCultureAndRadioButtonState();
            }
        }

        private void RestoreInitialCultureAndRadioButtonState()
        {

        }

        private void ValidateSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsViewModel.SaveSettings();
        }
    }
}
