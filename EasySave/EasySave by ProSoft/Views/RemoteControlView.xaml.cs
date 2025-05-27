using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// Interaction logic for RemoteControlView.xaml
    /// </summary>
    public partial class RemoteControlView : System.Windows.Controls.UserControl
    {
        private readonly RemoteControlViewModel _viewModel;

        public RemoteControlView()
        {
            InitializeComponent();
            _viewModel = new RemoteControlViewModel();
            DataContext = _viewModel;
            
            // Handle the follow job event
            _viewModel.FollowJob += ViewModel_FollowJob;
            
            // Register for unload to perform cleanup
            Unloaded += RemoteControlView_Unloaded;
        }
        private void ViewModel_FollowJob(JobStatus job)
        {
            // Find the job in the ListView and scroll to it
            if (JobsListView.ItemContainerGenerator.ContainerFromItem(job.BackupJob) is System.Windows.Controls.ListViewItem item)
            {
                item.Focus();
                JobsListView.ScrollIntoView(job.BackupJob);
            }
        }
        private void RemoteControlView_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.FollowJob -= ViewModel_FollowJob;
            Cleanup();
        }
        public void Cleanup()
        {
            _viewModel.Disconnect();
        }
    }
}