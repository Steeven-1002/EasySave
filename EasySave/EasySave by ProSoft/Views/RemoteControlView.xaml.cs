using EasySave_by_ProSoft.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

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
            
            // Create the view model
            _viewModel = new RemoteControlViewModel();
            DataContext = _viewModel;
            
            // Register for cleanup when the view is unloaded
            Unloaded += RemoteControlView_Unloaded;
        }

        private void RemoteControlView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Disconnect and clean up when the view is unloaded
            _viewModel.Cleanup();
        }
    }
}