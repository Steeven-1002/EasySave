using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Globalization;
using EasySave_by_ProSoft.Properties; // To access to Settings.Default
using System.Diagnostics;             // Necessary for Process
using EasySave_by_ProSoft.Localization;

namespace EasySave_by_ProSoft.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private string _initialCultureName;

        public SettingsView()
        {
            InitializeComponent();
            _initialCultureName = Thread.CurrentThread.CurrentUICulture.Name;
            // System.Windows.System.Windows.MessageBox.Show($"SettingsView Constructor: Initial culture is '{_initialCultureName}'", "Debug SettingsView Init");
            UpdateLanguageRadioButtons();
        }

        private void UpdateLanguageRadioButtons()
        {
            string currentCultureUI = Thread.CurrentThread.CurrentUICulture.Name;
            // System.Windows.System.Windows.MessageBox.Show($"UpdateLanguageRadioButtons: currentCultureUI is '{currentCultureUI}'", "Debug UpdateRadio");

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
                    System.Windows.MessageBox.Show($"Culture {selectedCultureName} non trouvée: {ex.Message}", Localization.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                System.Windows.MessageBox.Show(
                    $"Erreur critique en tentant de restaurer la culture initiale '{_initialCultureName}'.\n{cnfEx.Message}",
                    Localization.Resources.ErrorTitle,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void LoadSettings()
        {
            // Set up the log format based on current settings
            if (settings.GetSetting("LogFormat")?.ToString() == "JSON")
            {
                LogFormatComboBox.SelectedIndex = 0; // JSON
            }
            else
            {
                LogFormatComboBox.SelectedIndex = 1; // XML
            }

            // Load any other settings that need to be displayed
            if (!string.IsNullOrEmpty(settings.GetSetting("BusinessSoftwareName")?.ToString()))
            {
                BusinessSoftwareProcessNameTextBox.Text = settings.GetSetting("BusinessSoftwareName").ToString();
            }
            else
            {
                BusinessSoftwareProcessNameTextBox.Text = string.Empty;
            }
            
            if (settings.GetSetting("EncryptionExtensions") is List<string> extensions && extensions.Count > 0)
            {
                DefaultEncryptExtensionsTextBox.Text = string.Join(", ", extensions);
            }
            else
            {
                DefaultEncryptExtensionsTextBox.Text = string.Empty;
            }
        }

        private void ValidateSettings_Click(object sender, RoutedEventArgs e)
        {
            // Update log format based on selection
            string selectedFormat = LogFormatComboBox.SelectedIndex == 0 ? "JSON" : "XML";
            if (settings.GetSetting("LogFormat")?.ToString() != selectedFormat)
            {
                settings.SetSetting("LogFormat", selectedFormat);

                // Update the LoggingService with the new format
                LoggingService logger = LoggingService.Instance;
                string formatString = selectedFormat.ToString();
                LoggingService.RecreateInstance(selectedFormat);
            }

            // Update other settings
            settings.SetSetting("BusinessSoftwareName", BusinessSoftwareProcessNameTextBox.Text.Trim());

            // Parse encryption extensions
            string extensionsText = DefaultEncryptExtensionsTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(extensionsText))
            {
                settings.SetSetting("EncryptionExtensions", new List<string>(extensionsText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)));
            }
            else
            {
                settings.SetSetting("EncryptionExtensions", new List<string>());
            }
            
            // Save all settings
            settings.SaveConfiguration();

            System.Windows.MessageBox.Show(Localization.Resources.SettingsValidatedMessage,
                Localization.Resources.ConfirmationTitle,
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Save other settings if needed
            settings.SaveConfiguration();
            System.Windows.MessageBox.Show(Localization.Resources.SettingsSaved,
                Localization.Resources.Settings,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
