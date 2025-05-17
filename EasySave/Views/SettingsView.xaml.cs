// ...existing using statements...
using EasySave_by_ProSoft.ViewModels;

namespace EasySave_by_ProSoft.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsView()
        {
            InitializeComponent();
            ViewModel = new SettingsViewModel();
            this.DataContext = ViewModel;
            // ...existing code...
        }

        // ...existing methods...
    }
}