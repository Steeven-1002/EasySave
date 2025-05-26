using System; // Add this to resolve EventArgs
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using EasySave_by_ProSoft.ViewModels;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// Interaction logic for RemoteControlWindow.xaml
    /// </summary>
    public partial class RemoteControlWindow : Window
    {
        private readonly RemoteControlViewModel _viewModel;

        public RemoteControlWindow()
        {
            InitializeComponent();
            _viewModel = new RemoteControlViewModel();
            DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _viewModel.Disconnect();
        }
    }

    // Move converters to a separate file if they are reused, or keep them here if only used in this view.
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }
    }

    public class BoolToRunningConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "Running" : "Not Running";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value.ToString().Equals("Running");
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green; // Fully qualify Brushes to resolve ambiguity
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value.Equals(System.Windows.Media.Brushes.Red); // Fully qualify Brushes to resolve ambiguity
        }
    }
}