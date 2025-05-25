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
            {
                CreateJobPanel.DataContext = _jobAddViewModel;
            }

            // Make sure ListBox is properly bound to the Jobs collection
            if (BackupJobsListView != null)
            {
                BackupJobsListView.ItemsSource = _jobsListViewModel.Jobs;
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
            if (_jobsListViewModel != null)
            {
                _jobsListViewModel.LoadJobs();
            }
        }

        /// <summary>
        /// Launches a single selected backup job
        /// </summary>
        private void LaunchSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            // Check for job selection using the ViewModel method
            if (!_jobsListViewModel.ValidateJobSelection())
                return;

            if (_jobsListViewModel.SelectedJobs.Count == 1)
                _jobsListViewModel.LaunchJobCommand.Execute(null);
            else
                foreach (var job in _jobsListViewModel.SelectedJobs)
                {
                    _jobsListViewModel.LaunchJobCommand.Execute(job);
                }
        }

        private void DeleteSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            // The button will be disabled when no jobs are selected via the Command binding
            // But we still keep this method to notify about the deletion
            _jobsListViewModel.NotifyJobDeleted();
        }

        private void LaunchMultipleJobs_Click(object sender, RoutedEventArgs e)
        {
            // Get selected jobs via checkboxes
            var selectedJobs = _jobsListViewModel.Jobs.Where(job => job.IsSelected).ToList();

            if (selectedJobs.Count == 0)
            {
                _dialogService.ShowWarning("Please select at least one job to launch.");
                return;
            }

            _jobsListViewModel.LaunchMultipleJobsCommand.Execute(null);
            _jobsListViewModel.NotifyJobsLaunched(selectedJobs.Count);
        }

        private void CreateNewJob_Click(object sender, RoutedEventArgs e)
        {
            if (CreateJobPanel != null)
            {
                // Set default values for the job fields
                _jobAddViewModel.Name = string.Empty;
                _jobAddViewModel.SourcePath = string.Empty;
                _jobAddViewModel.TargetPath = string.Empty;
                _jobAddViewModel.Type = BackupType.Full;

                // Set the ComboBox selection to the default
                if (JobTypeComboBox != null)
                    JobTypeComboBox.SelectedIndex = 0;

                // Show the panel
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
            // Make sure job name is set correctly from TextBox before validation
            if (JobNameTextBox != null && !string.IsNullOrWhiteSpace(JobNameTextBox.Text))
                _jobAddViewModel.Name = JobNameTextBox.Text;

            if (JobSourcePathTextBox != null && !string.IsNullOrWhiteSpace(JobSourcePathTextBox.Text))
                _jobAddViewModel.SourcePath = JobSourcePathTextBox.Text;

            if (JobTargetPathTextBox != null && !string.IsNullOrWhiteSpace(JobTargetPathTextBox.Text))
                _jobAddViewModel.TargetPath = JobTargetPathTextBox.Text;

            // Set values in the view model
            if (!_jobAddViewModel.ValidateJobInputs())
                return;

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
                // Clear input fields before hiding the panel
                JobNameTextBox.Text = string.Empty;
                JobSourcePathTextBox.Text = string.Empty;
                JobTargetPathTextBox.Text = string.Empty;
                JobTypeComboBox.SelectedIndex = 0;

                // Reset ViewModel properties
                _jobAddViewModel.Name = string.Empty;
                _jobAddViewModel.SourcePath = string.Empty;
                _jobAddViewModel.TargetPath = string.Empty;
                _jobAddViewModel.Type = BackupType.Full;

                // Hide the panel
                CreateJobPanel.Visibility = Visibility.Collapsed;
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
            var selectedJobs = _jobsListViewModel.Jobs
                .Where(job => job.IsSelected)
                .ToList();

            // Update the view model
            _jobsListViewModel.SelectedJobs = selectedJobs;
        }

        private void PauseSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            // Check for job selection using the ViewModel method
            if (!_jobsListViewModel.ValidateJobSelection())
                return;

            if (!_jobsListViewModel.CanPauseSelectedJob())
            {
                _dialogService.ShowWarning("Selected jobs cannot be paused. Only running jobs can be paused.");
                return;
            }

            _jobsListViewModel.PauseJobCommand.Execute(null);
        }

        private void ResumeSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            // Check for job selection using the ViewModel method
            if (!_jobsListViewModel.ValidateJobSelection())
                return;

            if (!_jobsListViewModel.CanResumeSelectedJob())
            {
                _dialogService.ShowWarning("Selected jobs cannot be resumed. Only paused jobs can be resumed.");
                return;
            }

            _jobsListViewModel.ResumeJobCommand.Execute(null);
        }

        private void StopSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            // Check for job selection using the ViewModel method
            if (!_jobsListViewModel.ValidateJobSelection())
                return;

            var jobsToStop = _jobsListViewModel.SelectedJobs.Where(job => 
                job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused).ToList();
            
            if (jobsToStop.Count == 0)
            {
                _dialogService.ShowWarning("No running or paused jobs selected to stop.");
                return;
            }

            _jobsListViewModel.StopJobCommand.Execute(null);
        }

        // Update the code where ambiguity occurs
        private void JobPause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is BackupJob job)
            {
                if (job.Status.State == BackupState.Running)
                {
                    job.Pause();
                    _dialogService.ShowInformation($"Job '{job.Name}' has been paused.");
                }
            }
        }

        /// <summary>
        /// Resumes a specific job when its resume button is clicked
        /// </summary>
        private void JobResume_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is BackupJob job)
            {
                if (job.Status.State == BackupState.Paused)
                {
                    job.Resume();
                    _dialogService.ShowInformation($"Job '{job.Name}' has been resumed.");
                }
            }
        }

        /// <summary>
        /// Stops a specific job when its stop button is clicked
        /// </summary>
        private void JobStop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is BackupJob job)
            {
                if (job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused)
                {
                    job.Stop();
                    _dialogService.ShowInformation($"Job '{job.Name}' has been stopped.");
                }
            }
        }
    }
}