using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Properties; // For Settings.Default
using EasySave_by_ProSoft.Services;
using EasySave_by_ProSoft.Views;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EasySave_by_ProSoft
{
    public partial class App : System.Windows.Application
    {
        private const string DefaultCultureName = "en-US"; // Default language if nothing is saved or if error
        private static Mutex? _mutex;
        private const string MutexName = "EasySave_by_ProSoft_SingleInstanceMutex";
        private BackupManager _backupManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running, show a message and exit
                System.Windows.MessageBox.Show(
                    Localization.Resources.ApplicationAlreadyRunning, 
                    Localization.Resources.SingleInstanceTitle, 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
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
                    // Apply the default culture first to ensure we can use resources
                    CultureInfo defaultCulture = new CultureInfo(DefaultCultureName);
                    Thread.CurrentThread.CurrentUICulture = defaultCulture;
                    Thread.CurrentThread.CurrentCulture = defaultCulture;
                    
                    // The saved language is not valid, fallback to default language
                    System.Windows.MessageBox.Show(
                        string.Format(Localization.Resources.CultureNotFoundMessage, savedLang, DefaultCultureName),
                        Localization.Resources.InvalidCultureTitle);
                        
                    targetCulture = defaultCulture;
                    Settings.Default.UserLanguage = DefaultCultureName;
                    Settings.Default.Save(); // Save the default language
                }
            }
            else
            {
                // Apply the default culture first to ensure we can use resources
                targetCulture = new CultureInfo(DefaultCultureName);
                Thread.CurrentThread.CurrentUICulture = targetCulture;
                Thread.CurrentThread.CurrentCulture = targetCulture;
                
                // Any saved language is empty, use the default language and save it
                System.Windows.MessageBox.Show(
                    string.Format(Localization.Resources.NoLanguageSavedMessage, DefaultCultureName),
                    Localization.Resources.NoLanguageSavedTitle);
                    
                Settings.Default.UserLanguage = DefaultCultureName;
                Settings.Default.Save(); // Save the default language
            }

            ApplyCulture(targetCulture);

            // Initialize the BackupManager
            _backupManager = new BackupManager();
            
            // Start the remote control server automatically
            bool serverStarted = _backupManager.StartRemoteControlServer(9000);
            if (serverStarted)
            {
                Debug.WriteLine("Remote control server started automatically on application startup");
            }
            else
            {
                Debug.WriteLine("Failed to start remote control server on application startup");
            }

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
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();

            // Clean up resources
            _backupManager?.Shutdown();

            base.OnExit(e);
        }
    }

    // These converters were originally in RemoteControlWindow.xaml.cs
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }
    }

    public class BoolToRunningConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "Running" : "Not Running";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value.ToString().Equals("Running");
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value.Equals(System.Windows.Media.Brushes.Red);
        }
    }
}