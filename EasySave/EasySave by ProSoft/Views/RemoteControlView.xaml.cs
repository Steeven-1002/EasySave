using EasySave_by_ProSoft.Network;
using System;
using System.Diagnostics;
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
            _viewModel = new RemoteControlViewModel();
            DataContext = _viewModel;
            
            // Register for unload to perform cleanup
            Unloaded += RemoteControlView_Unloaded;
        }

        private void RemoteControlView_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        public void Cleanup()
        {
            _viewModel.Disconnect();
        }
    }
}