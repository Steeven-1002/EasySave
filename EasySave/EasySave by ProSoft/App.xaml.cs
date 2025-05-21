using EasySave_by_ProSoft.Properties; // For Settings.Default
using System.Globalization;
using System.Windows;
using System.Threading;

namespace EasySave_by_ProSoft
{
    public partial class App : System.Windows.Application
    {
        private const string DefaultCultureName = "en-US"; // Default language if nothing is saved or if error
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            string initialThreadCultureName = Thread.CurrentThread.CurrentUICulture.Name;
            string initialSavedLang = Settings.Default.UserLanguage;

            const string mutexName = "CryptoSoft_MonoInstance_Mutex";
            bool createdNew;
            _mutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew)
            {
                // Another instance is already running, show a message and exit
                System.Windows.MessageBox.Show("Another instance of the application is already running.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }


            string savedLang = Settings.Default.UserLanguage;
            CultureInfo targetCulture;

            if (!string.IsNullOrEmpty(savedLang))
            {
                try
                {
                    targetCulture = new CultureInfo(savedLang);
                }
                catch (CultureNotFoundException)
                {
                    // The saved language is invalid, use the default language and save it
                    System.Windows.MessageBox.Show($"App.OnStartup: Culture '{savedLang}' not found. Switching to default language '{DefaultCultureName}'.", "Debug Startup - Invalid Culture");
                    targetCulture = new CultureInfo(DefaultCultureName);
                    Settings.Default.UserLanguage = DefaultCultureName;
                    Settings.Default.Save(); // Save default language
                }
            }
            else
            {
                // No language saved (first launch), use default language and save it
                System.Windows.MessageBox.Show($"App.OnStartup: No language saved. Switching to default language '{DefaultCultureName}'.", "Debug Startup - No Language Saved");
                targetCulture = new CultureInfo(DefaultCultureName);
                Settings.Default.UserLanguage = DefaultCultureName;
                Settings.Default.Save(); // Save default language
            }

            ApplyCulture(targetCulture);

            base.OnStartup(e);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }


        private void ApplyCulture(CultureInfo culture)
        {
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            // Update the static Culture property of the generated resource class.
            if (Localization.Resources.Culture != null || Localization.Resources.Culture == null) // Allows assignment
            {
                Localization.Resources.Culture = culture;
            }
        }
    }
}