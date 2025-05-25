using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Services;
using EasySave_by_ProSoft.ViewModels;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace EasySave_by_ProSoft.Views
{
    /// <summary>
    /// View for managing backup jobs
    /// </summary>
    public partial class BackupJobsView : System.Windows.Controls.UserControl
    {
        private readonly BackupManager _backupManager;
        private readonly MainViewModel _backupJobsViewModel;
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
            _backupJobsViewModel = new MainViewModel(_backupManager, _dialogService);
            _jobAddViewModel = new JobAddViewModel(_backupManager, _dialogService);

            // Connect events
            _jobAddViewModel.JobAdded += _backupJobsViewModel.JobAdded;

            // Connect notification events
            _backupJobsViewModel.ShowErrorMessage += message => _dialogService.ShowError(message);
            _backupJobsViewModel.ShowInfoMessage += message => _dialogService.ShowInformation(message);
            _jobAddViewModel.ValidationError += message => _dialogService.ShowError(message);
            _jobAddViewModel.JobCreated += message => _dialogService.ShowInformation(message);

            // Set data contexts
            DataContext = _backupJobsViewModel;
            if (CreateJobPanel != null)
            {
                CreateJobPanel.DataContext = _jobAddViewModel;
            }

            // Make sure ListBox is properly bound to the Jobs collection
            if (BackupJobsListView != null)
            {
                BackupJobsListView.ItemsSource = _backupJobsViewModel.Jobs;
                BackupJobsListView.SelectionMode = System.Windows.Controls.SelectionMode.Extended;

                // You can keep this event handler for backwards compatibility or remove it
                // if you're fully migrating to the checkbox-based selection
                BackupJobsListView.SelectionChanged += (s, e) =>
                {
                    foreach (var item in e.RemovedItems)
                    {
                        if (item is BackupJob job && job.IsSelected)
                        {
                            // Don't uncheck the checkbox if the item is still selected in checkbox mode
                            e.Handled = true;
                            return;
                        }
                    }
                };
            }

            // Refresh jobs from JSON file
            RefreshJobsList();
        }

        /// <summary>
        /// Refreshes the jobs list from the JSON storage
        /// </summary>
        public void RefreshJobsList()
        {
            if (_backupJobsViewModel != null)
            {
                _backupJobsViewModel.LoadJobs();
            }
        }

        /// <summary>
        /// Launches a single selected backup job
        /// </summary>
        private void LaunchSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            // Check for job selection using the ViewModel method
            if (!_backupJobsViewModel.ValidateJobSelection())
                return;

            if (_backupJobsViewModel.SelectedJobs.Count == 1)
                _backupJobsViewModel.LaunchJobCommand.Execute(null);
            else
                foreach (var job in _backupJobsViewModel.SelectedJobs)
                {
                    _backupJobsViewModel.LaunchJobCommand.Execute(job);
                }
        }

        private void DeleteSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            if (!_backupJobsViewModel.ValidateJobSelection())
                return;

            _backupJobsViewModel.RemoveJobCommand.Execute(null);
            _backupJobsViewModel.NotifyJobDeleted();
        }

        private void LaunchMultipleJobs_Click(object sender, RoutedEventArgs e)
        {
            // Get selected jobs via checkboxes
            var selectedJobs = _backupJobsViewModel.Jobs.Where(job => job.IsSelected).ToList();

            if (selectedJobs.Count == 0)
            {
                _dialogService.ShowWarning("Please select at least one job to launch.");
                return;
            }

            _backupJobsViewModel.LaunchMultipleJobsCommand.Execute(null);
            _backupJobsViewModel.NotifyJobsLaunched(selectedJobs.Count);
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
        }

        private void BrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            string dialogCom = Localization.Resources.MessageSelectTargetFolder;
            JobTargetPathTextBox.Text = Browse_Click(sender, e, dialogCom);
        }

        /// <summary>
        /// Opens a folder browser dialog
        /// </summary>
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
            // Set values in the view model
            if (!_jobAddViewModel.ValidateJobInputs())
                return;

            _jobAddViewModel.SourcePath = JobSourcePathTextBox.Text;
            _jobAddViewModel.TargetPath = JobTargetPathTextBox.Text;
            _jobAddViewModel.Name = JobNameTextBox.Text;

            // Try to parse the backup type from the combo box
            try
            {
                _jobAddViewModel.Type = Enum.TryParse(typeof(BackupType), (JobTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString(), out var result)
                    ? (BackupType)result
                    : throw new InvalidOperationException("Invalid backup type selected.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error with backup type: {ex.Message}");
                return;
            }

            // Execute the view model command to create the job
            if (_jobAddViewModel.AddJobCommand.CanExecute(null))
            {
                _jobAddViewModel.AddJobCommand.Execute(null);

                // Hide the panel after validation
                if (CreateJobPanel != null)
                {
                    CreateJobPanel.Visibility = Visibility.Collapsed;
                }

                // Clear input fields
                JobNameTextBox.Text = string.Empty;
                JobSourcePathTextBox.Text = string.Empty;
                JobTargetPathTextBox.Text = string.Empty;
                JobTypeComboBox.SelectedIndex = 0;
            }
        }

        private void CancelNewJob_Click(object sender, RoutedEventArgs e)
        {
            if (CreateJobPanel != null)
            {
                CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel on cancel
            }
        }

        /// <summary>
        /// Event handler for checkbox change events
        /// </summary>
        private void BackupJob_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is BackupJob job)
            {
                UpdateSelectedJobsFromCheckboxes();
            }
        }

        /// <summary>
        /// Updates the view model's selected jobs based on checkbox states
        /// </summary>
        private void UpdateSelectedJobsFromCheckboxes()
        {
            // Get all jobs that have IsSelected = true
            var selectedJobs = _backupJobsViewModel.Jobs
                .Where(job => job.IsSelected)
                .ToList();

            // Update the view model
            _backupJobsViewModel.SelectedJobs = selectedJobs;
        }
    }
}