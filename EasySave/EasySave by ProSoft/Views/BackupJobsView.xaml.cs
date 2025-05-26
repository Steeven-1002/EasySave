using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Services;
using EasySave_by_ProSoft.ViewModels;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;
using System.Linq;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// View for managing backup jobs
    /// </summary>
    public partial class BackupJobsView : System.Windows.Controls.UserControl
    {
        private readonly BackupManager _backupManager;
        private readonly JobsListViewModel _jobsListViewModel;
        private readonly JobAddViewModel _jobAddViewModel;
        private readonly IDialogService _dialogService;

        public BackupJobsView()
        {
            InitializeComponent();
            _dialogService = new DialogService();
        }

        public BackupJobsView(BackupManager backupManager) : this()
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));

            // Create ViewModels with the BackupManager
            _jobsListViewModel = new JobsListViewModel(_backupManager, _dialogService);
            _jobAddViewModel = new JobAddViewModel(_backupManager, _dialogService);

            // Connect events
            _jobAddViewModel.JobAdded += _jobsListViewModel.JobAdded;

            // Connect notification events
            _jobsListViewModel.ValidationError += message => _dialogService.ShowError(message);
            _jobsListViewModel.JobStatusChanged += message => _dialogService.ShowInformation(message);
            _jobAddViewModel.ValidationError += message => _dialogService.ShowError(message);
            _jobAddViewModel.JobCreated += message => _dialogService.ShowInformation(message);

            // Set data contexts
            DataContext = _jobsListViewModel;
            if (CreateJobPanel != null)
                CreateJobPanel.DataContext = _jobAddViewModel;

            // Bind ListBox to Jobs collection (no event handler needed for selection)
            if (BackupJobsListView != null)
            {
                BackupJobsListView.ItemsSource = _jobsListViewModel.Jobs;
                BackupJobsListView.SelectionMode = System.Windows.Controls.SelectionMode.Extended;
            }

            // Refresh jobs from JSON file
            RefreshJobsList();
        }

        /// <summary>
        /// Refreshes the jobs list from the JSON storage
        /// </summary>
        public void RefreshJobsList()
        {
            _jobsListViewModel?.LoadJobs();
        }

        /// <summary>
        /// Opens a folder browser dialog
        /// </summary>
        private string Browse_Click(object sender, RoutedEventArgs e, string com)
        {
            var dialog = new WinForms.FolderBrowserDialog();
            dialog.Description = com;
            WinForms.DialogResult result = dialog.ShowDialog();

            if (result == WinForms.DialogResult.OK)
                return dialog.SelectedPath;
            return string.Empty;
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            string dialogCom = Localization.Resources.MessageSelectSourceFolder;
            JobSourcePathTextBox.Text = Browse_Click(sender, e, dialogCom);
        }

        private void BrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            string dialogCom = Localization.Resources.MessageSelectTargetFolder;
            JobTargetPathTextBox.Text = Browse_Click(sender, e, dialogCom);
        }
    }
}