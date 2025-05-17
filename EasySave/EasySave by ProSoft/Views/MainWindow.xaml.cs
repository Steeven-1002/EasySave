using System.Globalization;
using System.Threading;
using System.Windows;
using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.ViewModels;
using EasySave_by_ProSoft.Views;

namespace EasySave_by_ProSoft
{
    /// <summary>
    /// Main window of the application
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackupManager _backupManager;
        private BackupJobsView _backupJobsView;
        private SettingsView _settingsView;

        public MainWindow()
        {
            // Create the BackupManager instance to be shared between ViewModels
            _backupManager = new BackupManager();
            
            InitializeComponent();
            
            // Create views with the BackupManager
            _backupJobsView = new BackupJobsView(_backupManager);
            _settingsView = new SettingsView();

            // Show the default view
            MainContentArea.Content = _backupJobsView;
        }

        private void ShowBackupJobs_Click(object sender, RoutedEventArgs e)
        {
            // Refresh jobs list when switching to backup jobs view
            _backupJobsView.RefreshJobsList();
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