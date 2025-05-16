using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Globalization;
using EasySave_by_ProSoft.Properties; // To access to Settings.Default
using System.Diagnostics;             // Necessary for Process
using EasySave_by_ProSoft.Localization;

namespace EasySave_by_ProSoft.Views
{
    public partial class SettingsView : UserControl
    {
        private string _initialCultureName;

        public SettingsView()
        {
            InitializeComponent();
            _initialCultureName = Thread.CurrentThread.CurrentUICulture.Name;
            // System.Windows.MessageBox.Show($"SettingsView Constructor: Initial culture is '{_initialCultureName}'", "Debug SettingsView Init");
            UpdateLanguageRadioButtons();
        }

        private void UpdateLanguageRadioButtons()
        {
            string currentCultureUI = Thread.CurrentThread.CurrentUICulture.Name;
            // System.Windows.MessageBox.Show($"UpdateLanguageRadioButtons: currentCultureUI is '{currentCultureUI}'", "Debug UpdateRadio");

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
            if (sender is RadioButton radioButton && radioButton.IsChecked == true && radioButton.Tag != null)
            {
                string selectedCultureName = radioButton.Tag.ToString();
                
                if (selectedCultureName == Settings.Default.UserLanguage && selectedCultureName == Thread.CurrentThread.CurrentUICulture.Name)
                {
                    // System.Windows.MessageBox.Show($"LanguageRadioButton_Checked: Selected language ('{selectedCultureName}') is already active and saved. No action.", "Debug No Change Needed");
                    return;
                }

        
                Settings.Default.UserLanguage = selectedCultureName;
                Settings.Default.Save();

                try
                {
                    CultureInfo newCulture = new CultureInfo(selectedCultureName);
                    Thread.CurrentThread.CurrentUICulture = newCulture;
                    Thread.CurrentThread.CurrentCulture = newCulture;
                    if (Localization.Resources.Culture != null || Localization.Resources.Culture == null)
                    {
                        Localization.Resources.Culture = newCulture;
                    }
                }
                catch (CultureNotFoundException ex)
                {
                    MessageBox.Show($"Culture {selectedCultureName} non trouvée: {ex.Message}", Localization.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    Settings.Default.UserLanguage = _initialCultureName;
                    Settings.Default.Save();
                    RestoreInitialCultureAndRadioButtonState();
                    return;
                }
                PromptForApplicationRestart();
            }
        }

        private void PromptForApplicationRestart()
        {
            MessageBoxResult result = MessageBox.Show(
                Localization.Resources.LanguageChangeRestartMessage,
                Localization.Resources.ConfirmationTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string applicationPath = Process.GetCurrentProcess().MainModule.FileName;
                    if (string.IsNullOrEmpty(applicationPath))
                    {
                        applicationPath = Application.ResourceAssembly.Location;
                    }
                    Process.Start(applicationPath);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"{Localization.Resources.ErrorRestartingApplicationMessage}\n{ex.Message}",
                        Localization.Resources.ErrorTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Settings.Default.UserLanguage = _initialCultureName;
                    Settings.Default.Save();
                    RestoreInitialCultureAndRadioButtonState();
                }
            }
            else
            {
                MessageBox.Show(
                    Localization.Resources.LanguageChangeCancelledMessage,
                    Localization.Resources.InformationTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Settings.Default.UserLanguage = _initialCultureName;
                Settings.Default.Save();
                RestoreInitialCultureAndRadioButtonState();
            }
        }

        private void RestoreInitialCultureAndRadioButtonState()
        {
            try
            {
                CultureInfo initialCulture = new CultureInfo(_initialCultureName);
                Thread.CurrentThread.CurrentUICulture = initialCulture;
                Thread.CurrentThread.CurrentCulture = initialCulture;
                if (Localization.Resources.Culture != null || Localization.Resources.Culture == null)
                {
                    Localization.Resources.Culture = initialCulture;
                }
                UpdateLanguageRadioButtons();
            }
            catch (CultureNotFoundException cnfEx)
            {
                MessageBox.Show(
                    $"Erreur critique en tentant de restaurer la culture initiale '{_initialCultureName}'.\n{cnfEx.Message}",
                    Localization.Resources.ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ValidateSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(Localization.Resources.SettingsValidatedMessage,
                            Localization.Resources.ConfirmationTitle,
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
