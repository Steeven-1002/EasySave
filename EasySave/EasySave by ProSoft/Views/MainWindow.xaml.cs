using System.Globalization;
using System.Threading;
using System.Windows;
using EasySave_by_ProSoft.Views;

namespace EasySave_by_ProSoft.Views
{
    public partial class MainWindow : Window
    {
        private BackupJobsView _backupJobsView;
        private SettingsView _settingsView;

        public MainWindow()
        {
            InitializeComponent();
            _backupJobsView = new BackupJobsView();
            _settingsView = new SettingsView();

            // Show the default view
            MainContentArea.Content = _backupJobsView;
        }

        private void ShowBackupJobs_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = _backupJobsView;
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = _settingsView;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}