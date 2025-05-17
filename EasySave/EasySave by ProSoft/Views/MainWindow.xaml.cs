using System.Globalization;
using System.Threading;
using System.Windows;
using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.ViewModels;
using EasySave_by_ProSoft.Views;

namespace EasySave_by_ProSoft
{
    public partial class MainWindow : Window
    {
        private readonly BackupManager _backupManager;
        private BackupJobsView _backupJobsView;
        private SettingsView _settingsView;

        public MainWindow()
        {
            // Créer l'instance du BackupManager qui sera partagée entre les ViewModels
            _backupManager = new BackupManager();
            
            InitializeComponent();
            
            // Créer les vues avec le BackupManager
            _backupJobsView = new BackupJobsView(_backupManager);
            _settingsView = new SettingsView();

            // Afficher la vue par défaut
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