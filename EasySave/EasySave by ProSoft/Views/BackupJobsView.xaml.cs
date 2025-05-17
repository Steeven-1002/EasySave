using System.Windows; // For RoutedEventArgs and MessageBox
using System.Windows.Controls; // For UserControl
using EasySave_by_ProSoft.Localization;
using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using WinForms = System.Windows.Forms;


namespace EasySave_by_ProSoft.Views
{
    public partial class BackupJobsView : System.Windows.Controls.UserControl
    {
        private readonly BackupManager _backupManager;
        private readonly BackupJobsViewModel _backupJobsViewModel;
        private readonly JobAddViewModel _jobAddViewModel;
        
        public BackupJobsView()
        {
            InitializeComponent();
        }
        
        public BackupJobsView(BackupManager backupManager) : this()
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            
            // Créer les ViewModels avec le BackupManager
            _backupJobsViewModel = new BackupJobsViewModel(_backupManager);
            _jobAddViewModel = new JobAddViewModel(_backupManager);
            
            // Connecter les événements
            _jobAddViewModel.JobAdded += _backupJobsViewModel.JobAdded;
            
            // Définir les contextes de données
            DataContext = _backupJobsViewModel;
            if (CreateJobPanel != null)
            {
                CreateJobPanel.DataContext = _jobAddViewModel;
            }
        }

        private void LaunchSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            _backupJobsViewModel.LaunchJobCommand.Execute(null);
            System.Windows.MessageBox.Show(Localization.Resources.MessageBoxLaunchJob);
        }

        private void CreateNewJob_Click(object sender, RoutedEventArgs e)
        {
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Visible;
            }
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string dialogCom = Localization.Resources.MessageSelectSourceFolder;
            JobSourcePathTextBox.Text = Browse_Click(sender, e, dialogCom);
            if (_jobAddViewModel != null)
            {
                _jobAddViewModel.SourcePath = JobSourcePathTextBox.Text;
            }
        }

        private void BrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            string dialogCom = Localization.Resources.MessageSelectTargetFolder;
            JobTargetPathTextBox.Text = Browse_Click(sender, e, dialogCom);
            if (_jobAddViewModel != null)
            {
                _jobAddViewModel.TargetPath = JobTargetPathTextBox.Text;
            }
        }

        private string Browse_Click(object sender, RoutedEventArgs e, string com)
        {
            var dialog = new WinForms.FolderBrowserDialog();
            dialog.Description = com; // Set the description based on the button clicked
            WinForms.DialogResult result = dialog.ShowDialog();

            if (result == WinForms.DialogResult.OK)
            {
                string selectedFilePath = dialog.SelectedPath;
                return selectedFilePath;
            }
            return string.Empty; // Return empty string if no folder is selected
        }

        private void ValidateNewJob_Click(object sender, RoutedEventArgs e)
        {
            // Exécuter la commande du ViewModel pour créer le job
            if (_jobAddViewModel != null && _jobAddViewModel.AddJobCommand.CanExecute(null))
            {
                _jobAddViewModel.AddJobCommand.Execute(null);
                System.Windows.MessageBox.Show(Localization.Resources.MessageNewJobValidated);
                
                if (CreateJobPanel != null)
                {
                    CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel after validation
                }
            }
        }

        private void CancelNewJob_Click(object sender, RoutedEventArgs e)
        {
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel on cancel
            }
        }       
    }
}