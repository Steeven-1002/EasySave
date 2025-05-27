using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Views;
using System.Windows;
using System.Windows.Controls;

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
        private RemoteControlServerView _remoteControlServerView;
        private RemoteControlView _remoteControlView;

        public MainWindow()
        {
            // Create the BackupManager instance to be shared between ViewModels
            _backupManager = new BackupManager();

            InitializeComponent();

            // Create views with the BackupManager
            _backupJobsView = new BackupJobsView(_backupManager);
            _settingsView = new SettingsView();
            _remoteControlServerView = new RemoteControlServerView(_backupManager);
            _remoteControlView = new RemoteControlView();

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

        private void ShowRemoteControl_Click(object sender, RoutedEventArgs e)
        {
            // Create dialog to choose between server and client mode
            var dialog = new Window
            {
                Title = "Remote Control Mode",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            var label = new TextBlock
            {
                Text = "Choose Remote Control Mode:",
                Margin = new Thickness(0, 0, 0, 20),
                FontSize = 16
            };
            panel.Children.Add(label);

            var serverButton = new System.Windows.Controls.Button
            {
                Content = "Server Mode (Accept Connections)",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            serverButton.Click += (s, args) =>
            {
                MainContentArea.Content = _remoteControlServerView;
                dialog.Close();
            };
            panel.Children.Add(serverButton);

            var clientButton = new System.Windows.Controls.Button
            {
                Content = "Client Mode (Connect to Server)",
                Padding = new Thickness(10)
            };
            clientButton.Click += (s, args) =>
            {
                MainContentArea.Content = _remoteControlView;
                dialog.Close();
            };
            panel.Children.Add(clientButton);

            dialog.Content = panel;
            dialog.ShowDialog();
        }
    }
}