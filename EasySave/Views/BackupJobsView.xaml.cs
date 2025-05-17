// ...existing using statements...
using EasySave_by_ProSoft.ViewModels;

namespace EasySave_by_ProSoft.Views
{
    public partial class BackupJobsView : System.Windows.Controls.UserControl
    {
        public BackupJobsViewModel ViewModel { get; }

        public BackupJobsView()
        {
            InitializeComponent();
            ViewModel = new BackupJobsViewModel();
            this.DataContext = ViewModel;
        }

        // ...existing event handlers...
    }
}