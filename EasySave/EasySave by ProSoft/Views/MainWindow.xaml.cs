﻿using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Views;
using System.Windows;
using EasySave_by_ProSoft.Services;


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
        private RemoteControlView _remoteControlView;
        private UIElement _currentView;
        private readonly IDialogService _dialogService = new DialogService();


        public MainWindow()
        {
            // Create the BackupManager instance to be shared between ViewModels
            _backupManager = BackupManager.Instance;

            InitializeComponent();

            // Create views with the BackupManager
            _backupJobsView = new BackupJobsView(_backupManager);
            _settingsView = new SettingsView();
            _remoteControlView = new RemoteControlView();

            // Show the default view
            MainContentArea.Content = _backupJobsView;
            _currentView = _backupJobsView;
        }

        private void ShowBackupJobs_Click(object sender, RoutedEventArgs e)
        {
            MainContentArea.Content = _backupJobsView;
            _currentView = _backupJobsView;
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_dialogService.ShowYesNoDialog(Localization.Resources.BackupJobsSettingsChangeConfirmation))
            {
                MainContentArea.Content = _settingsView;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void ShowRemoteControl_Click(object sender, RoutedEventArgs e)
        {
            // Connect directly to client view, since server is always running in background
            MainContentArea.Content = _remoteControlView;
            _currentView = _remoteControlView;
        }
    }
}