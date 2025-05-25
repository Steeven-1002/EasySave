using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Properties; // For Settings.Default
using System.Globalization;
using System.Windows;


namespace EasySave_by_ProSoft
{
    public partial class App : System.Windows.Application
    {
        private const string DefaultCultureName = "en-US"; // Default language if nothing is saved or if error
        private static Mutex? _mutex;
        private const string MutexName = "EasySave_by_ProSoft_SingleInstanceMutex";


        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running, show a message and exit
                System.Windows.MessageBox.Show("L'application est déjà en cours d'exécution.", "Instance unique", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            string initialThreadCultureName = Thread.CurrentThread.CurrentUICulture.Name;
            string initialSavedLang = Settings.Default.UserLanguage;

            string savedLang = Settings.Default.UserLanguage;
            CultureInfo targetCulture;
            JobEventManager.Instance.AddListener(LoggingService.Instance);

            if (!string.IsNullOrEmpty(savedLang))
            {
                try
                {
                    targetCulture = new CultureInfo(savedLang);
                }
                catch (CultureNotFoundException)
                {
                    //The saved language is not valid, fallback to default language
                    System.Windows.MessageBox.Show($"App.OnStartup: Culture '{savedLang}' non trouvée. Passage à la langue par défaut '{DefaultCultureName}'.", "Debug Startup - Culture invalide");
                    targetCulture = new CultureInfo(DefaultCultureName);
                    Settings.Default.UserLanguage = DefaultCultureName;
                    Settings.Default.Save(); // Save the default language
                }
            }
            else
            {
                // Any saved language is empty, use the default language and save it
                System.Windows.MessageBox.Show($"App.OnStartup: Aucune langue sauvegardée. Passage à la langue par défaut '{DefaultCultureName}'.", "Debug Startup - Aucune langue sauvegardée");
                targetCulture = new CultureInfo(DefaultCultureName);
                Settings.Default.UserLanguage = DefaultCultureName;
                Settings.Default.Save(); // Save the default language
            }

            ApplyCulture(targetCulture);

            base.OnStartup(e);
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

        /// <summary>
        /// Handles the application exit event to release the mutex.
        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }

    }
}
