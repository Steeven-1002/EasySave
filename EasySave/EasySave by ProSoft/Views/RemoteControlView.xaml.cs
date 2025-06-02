using EasySave_by_ProSoft.ViewModels;
using System.Windows;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// Interaction logic for RemoteControlView.xaml
    /// </summary>
    public partial class RemoteControlView : System.Windows.Controls.UserControl, IDisposable
    {
        private readonly RemoteControlViewModel _viewModel;
        private bool _disposed = false;

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
            // Dispose resources when the view is unloaded
            Dispose();
        }

        // IDisposable implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unregister from events
                    Unloaded -= RemoteControlView_Unloaded;

                    // Dispose the view model
                    _viewModel?.Dispose();
                }

                _disposed = true;
            }
        }

        ~RemoteControlView()
        {
            Dispose(false);
        }
    }
}