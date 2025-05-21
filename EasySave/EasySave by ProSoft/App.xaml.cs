using EasySave_by_ProSoft.Properties; // For Settings.Default
using System.Globalization;
using System.Threading;
using System.Windows;

namespace EasySave_by_ProSoft
{
    public partial class App : System.Windows.Application
    {
        private const string DefaultCultureName = "en-US";
        private static Mutex? _mutex;
        private const string MutexName = "CryptoSoft_MonoInstance_Mutex";

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            try
            {
                // Wait for the mutex for 2 seconds
                if (!_mutex.WaitOne(TimeSpan.FromSeconds(2), false))
                {
                    System.Windows.MessageBox.Show(
                        "Une autre instance du logiciel est déjà en cours d'exécution ou un verrou n'a pas été libéré correctement.\n\n" +
                        "Si vous pensez que ce n'est pas le cas, redémarrez votre ordinateur.",
                        "Erreur - Instance unique",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    Environment.Exit(0);
                }
            }
            catch (AbandonedMutexException)
            {
                // This exception is thrown if the mutex was abandoned by another thread
                System.Windows.MessageBox.Show(
                    "Une instance précédente de l'application s'est arrêtée de manière inattendue.\n" +
                    "Le logiciel va continuer normalement.",
                    "Avertissement - Instance récupérée",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }

            // Load the user language from settings
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
                    System.Windows.MessageBox.Show($"Culture '{savedLang}' introuvable. Passage à la langue par défaut '{DefaultCultureName}'.", "Erreur de langue");
                    targetCulture = new CultureInfo(DefaultCultureName);
                    Settings.Default.UserLanguage = DefaultCultureName;
                    Settings.Default.Save();
                }
            }
            else
            {
                System.Windows.MessageBox.Show($"Aucune langue sauvegardée. Passage à la langue par défaut '{DefaultCultureName}'.", "Langue par défaut");
                targetCulture = new CultureInfo(DefaultCultureName);
                Settings.Default.UserLanguage = DefaultCultureName;
                Settings.Default.Save();
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

            if (Localization.Resources.Culture != null || Localization.Resources.Culture == null)
            {
                Localization.Resources.Culture = culture;
            }
        }
    }
}
