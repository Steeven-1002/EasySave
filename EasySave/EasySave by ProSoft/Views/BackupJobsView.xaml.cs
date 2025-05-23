﻿using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.ViewModels;
using System.Windows; // For RoutedEventArgs and MessageBox
using System.Windows.Controls; // For UserControl
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

        public BackupJobsView()
        {
            InitializeComponent();
        }

        public BackupJobsView(BackupManager backupManager) : this()
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));

            // Create ViewModels with the BackupManager
            _backupJobsViewModel = new MainViewModel(_backupManager);
            _jobAddViewModel = new JobAddViewModel(_backupManager);

            // Connect events
            _jobAddViewModel.JobAdded += _backupJobsViewModel.JobAdded;

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
            // Check for selected jobs via checkboxes
            if (_backupJobsViewModel.SelectedJob == null || _backupJobsViewModel.SelectedJob.Count == 0)
            {
                System.Windows.MessageBox.Show("No backup selected");
                return;
            }
            else if (_backupJobsViewModel.SelectedJob.Count == 1)
                // Run the command with the selected job
                _backupJobsViewModel.LaunchJobCommand.Execute(null);
            else
                // Run the command with all selected jobs
                foreach (var job in _backupJobsViewModel.SelectedJob)
                {
                    _backupJobsViewModel.LaunchJobCommand.Execute(job);
                }
            // Update the UI to reflect the job status
            RefreshJobsList();
        }

        private void DeleteSelectedJob_Click(object sender, RoutedEventArgs e)
        {
            if (_backupJobsViewModel.SelectedJob == null)
            {
                System.Windows.MessageBox.Show("Please select a job to delete.");
                return;
            }

            _backupJobsViewModel.RemoveJobCommand.Execute(null);

            System.Windows.MessageBox.Show(Localization.Resources.MessageBoxDeleteJob);
        }

        private void LaunchMultipleJobs_Click(object sender, RoutedEventArgs e)
        {
            // Get selected jobs via checkboxes
            var selectedJobs = _backupJobsViewModel.Jobs.Where(job => job.IsSelected).ToList();

            if (selectedJobs.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select at least one job to launch.");
                return;
            }

            _backupJobsViewModel.LaunchMultipleJobsCommand.Execute(null);
            System.Windows.MessageBox.Show($"{selectedJobs.Count} jobs have been launched.");
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
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        /// <param name="com">Dialog description</param>
        /// <returns>Selected folder path or empty string</returns>
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
            // Execute the ViewModel command to create the job
            if (string.IsNullOrWhiteSpace(JobNameTextBox.Text) || string.IsNullOrWhiteSpace(JobSourcePathTextBox.Text) || string.IsNullOrWhiteSpace(JobTargetPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("Invalid input. Please fill all fields.");
                return;
            }
            _jobAddViewModel.SourcePath = JobSourcePathTextBox.Text;
            _jobAddViewModel.TargetPath = JobTargetPathTextBox.Text;
            _jobAddViewModel.Name = JobNameTextBox.Text;
            _jobAddViewModel.Type = Enum.TryParse(typeof(BackupType), (JobTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString(), out var result)
                ? (BackupType)result
                : throw new InvalidOperationException("Invalid backup type selected.");
            if (_jobAddViewModel != null && _jobAddViewModel.AddJobCommand.CanExecute(null))
            {
                _jobAddViewModel.AddJobCommand.Execute(null);
                System.Windows.MessageBox.Show(Localization.Resources.MessageNewJobValidated);

                if (CreateJobPanel != null)
                {
                    CreateJobPanel.Visibility = Visibility.Collapsed; // Hide the panel after validation
                }

                // Clear input fields
                JobNameTextBox.Text = string.Empty;
                JobSourcePathTextBox.Text = string.Empty;
                JobTargetPathTextBox.Text = string.Empty;
                JobTypeComboBox.SelectedIndex = 0;

                // Refresh the jobs list after adding a new job
                RefreshJobsList();
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
            _backupJobsViewModel.SelectedJob = selectedJobs.Count > 0 ? selectedJobs : null;
            _backupJobsViewModel.SelectedJobs = selectedJobs;
        }
    }
}