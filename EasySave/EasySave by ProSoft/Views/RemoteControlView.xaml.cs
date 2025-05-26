using EasySave_by_ProSoft.Network;
using EasySave_by_ProSoft.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// Interaction logic for RemoteControlView.xaml
    /// </summary>
    public partial class RemoteControlView : System.Windows.Controls.UserControl
    {
        private readonly RemoteControlViewModel _viewModel;
        private DispatcherTimer _connectionCheckTimer;

        public RemoteControlView()
        {
            InitializeComponent();
            _viewModel = new RemoteControlViewModel();
            DataContext = _viewModel;
            
            // Setup connection check timer to periodically verify socket state
            _connectionCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _connectionCheckTimer.Tick += ConnectionCheckTimer_Tick;
            _connectionCheckTimer.Start();
            
            // Handle the follow job event
            _viewModel.FollowJob += ViewModel_FollowJob;
            
            // Register for unload to perform cleanup
            Unloaded += RemoteControlView_Unloaded;
        }
        
        private void ViewModel_FollowJob(RemoteJobStatus job)
        {
            // Find the job in the ListView and scroll to it
            if (JobsListView.ItemContainerGenerator.ContainerFromItem(job) is System.Windows.Controls.ListViewItem item)
            {
                item.Focus();
                JobsListView.ScrollIntoView(job);
            }
        }
        
        private void ConnectionCheckTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Ping for status update if connected
                if (_viewModel.IsConnected && !_viewModel.IsOperationInProgress)
                {
                    _viewModel.CheckConnectionStatus();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application on connection check
                Debug.WriteLine($"Error in connection check timer: {ex.Message}");
            }
        }

        private void RemoteControlView_Unloaded(object sender, RoutedEventArgs e)
        {
            _connectionCheckTimer.Stop();
            _viewModel.FollowJob -= ViewModel_FollowJob;
            Cleanup();
        }

        public void Cleanup()
        {
            _viewModel.Disconnect();
        }
    }
}