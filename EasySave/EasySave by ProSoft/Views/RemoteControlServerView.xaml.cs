using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// Interaction logic for RemoteControlServerView.xaml
    /// </summary>
    public partial class RemoteControlServerView : System.Windows.Controls.UserControl
    {
        private readonly RemoteControlServerViewModel _viewModel;

        public RemoteControlServerView()
        {
            InitializeComponent();
        }

        public RemoteControlServerView(BackupManager backupManager) : this()
        {
            _viewModel = new RemoteControlServerViewModel(backupManager);
            DataContext = _viewModel;

            // Register for cleanup when the view is unloaded
            Unloaded += RemoteControlServerView_Unloaded;
        }

        private void RemoteControlServerView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Cleanup resources when the view is unloaded
            _viewModel.Cleanup();
        }
    }
}