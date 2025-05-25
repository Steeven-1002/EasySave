using EasySave_by_ProSoft.Network;
using System.Windows;
using System.Windows.Data;

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

    // Add these converters to App.xaml resources if not already there
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
            return (bool)value ? Brushes.Red : Brushes.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value.Equals(Brushes.Red);
        }
    }
}