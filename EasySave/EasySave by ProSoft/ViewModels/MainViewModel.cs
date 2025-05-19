using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// View model for managing backup jobs
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        private ObservableCollection<BackupJob> _jobs;
        private DispatcherTimer _statusUpdateTimer;

        public ObservableCollection<BackupJob> Jobs
        {
            get => _jobs;
            set
            {
                _jobs = value;
                OnPropertyChanged();
            }
        }

        private List<BackupJob>? _selectedJob;
        public List<BackupJob>? SelectedJob
        {
            get => _selectedJob;
            set
            {
                _selectedJob = value;
                OnPropertyChanged();
                // Notify commands that depend on selection
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private List<BackupJob> _selectedJobs = new List<BackupJob>();
        /// <summary>
        /// Gets or sets the list of selected backup jobs for multiple execution
        /// </summary>
        public List<BackupJob> SelectedJobs
        {
            get => _selectedJobs;
            set
            {
                _selectedJobs = value;
                OnPropertyChanged();
                // Notify commands that depend on selection
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand CreateJobCommand { get; }
        public ICommand LaunchJobCommand { get; }
        public ICommand LaunchMultipleJobsCommand { get; }
        public ICommand RemoveJobCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }

        public MainViewModel(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _jobs = new ObservableCollection<BackupJob>();

            CreateJobCommand = new RelayCommand(_ => CreateJob(), _ => true);
            LaunchJobCommand = new RelayCommand(_ => LaunchSelectedJob(), _ => SelectedJob != null && SelectedJob.Count > 0);
            LaunchMultipleJobsCommand = new RelayCommand(_ => LaunchMultipleJobs(), _ => SelectedJobs != null && SelectedJobs.Count > 0);
            RemoveJobCommand = new RelayCommand(_ => RemoveSelectedJob(), _ => SelectedJob != null && SelectedJob.Count > 0);
            PauseJobCommand = new RelayCommand(_ => PauseSelectedJob(), _ => CanPauseSelectedJob());
            ResumeJobCommand = new RelayCommand(_ => ResumeSelectedJob(), _ => CanResumeSelectedJob());

            // Configure timer to refresh job status every second
            _statusUpdateTimer = new DispatcherTimer();
            _statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
        }

        /// <summary>
        /// Timer callback to refresh job status information
        /// </summary>
        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            // Create a copy of the collection to iterate over
            var jobsCopy = Jobs.ToList();

            foreach (var job in jobsCopy)
            {
                OnPropertyChanged(nameof(Jobs));

                var tempJob = job;
                if (tempJob != null)
                {
                    var index = Jobs.IndexOf(tempJob);
                    if (index >= 0)
                    {
                        Jobs[index] = tempJob; // Replace without removing
                    }
                }
            }
        }

        /// <summary>
        /// Loads all jobs from the BackupManager and updates the observable collection
        /// </summary>
        public void LoadJobs()
        {
            // Save currently selected job IDs to restore selection after refresh
            var selectedJobIds = SelectedJobs?.Select(j => j.Name).ToList() ?? new List<string>();

            // Ensure BackupManager has the latest data from JSON
            _backupManager.LoadJobs();

            // Get the updated job list
            var jobs = _backupManager.GetAllJobs();

            // Clear and reload jobs
            Jobs.Clear();
            foreach (var job in jobs)
            {
                // Restore selection state
                job.IsSelected = selectedJobIds.Contains(job.Name);
                Jobs.Add(job);

                // Ensure we update selection in the view model too
                if (job.IsSelected)
                {
                    if (SelectedJobs == null)
                        SelectedJobs = new List<BackupJob>();

                    if (!SelectedJobs.Contains(job))
                        SelectedJobs.Add(job);
                }
            }

            // Keep selection synchronized
            SelectedJob = SelectedJobs;

            OnPropertyChanged(nameof(Jobs));
            OnPropertyChanged(nameof(SelectedJob));
            OnPropertyChanged(nameof(SelectedJobs));
        }

        /// <summary>
        /// Updates the selection based on checkbox states
        /// </summary>
        public void UpdateSelectionFromCheckboxes()
        {
            var selected = Jobs.Where(j => j.IsSelected).ToList();
            SelectedJobs = selected;
            SelectedJob = selected.Count > 0 ? selected : null;
        }

        /// <summary>
        /// Adds a job to the observable collection and sets up property change notifications
        /// </summary>
        /// <param name="job">The job to add</param>
        public void JobAdded(BackupJob job)
        {
            if (!Jobs.Contains(job))
            {
                // Listen for property changes on the job's status
                if (job.Status is INotifyPropertyChanged statusNotifier)
                {
                    statusNotifier.PropertyChanged += (s, e) => 
                    {
                        // Force UI refresh when job status changes
                        OnPropertyChanged(nameof(Jobs));
                    };
                }
                
                Jobs.Add(job);
                OnPropertyChanged(nameof(Jobs));
            }
        }

        /// <summary>
        /// Opens the job creation panel
        /// </summary>
        private void CreateJob()
        {
            // This method can be used to display a job creation dialog
            // See the equivalent method in BackupJobsView.xaml.cs (CreateNewJob_Click)
        }

        /// <summary>
        /// Launches the selected backup job
        /// </summary>
        private void LaunchSelectedJob()
        {
            if (SelectedJob != null)
            {
                foreach (var job in SelectedJob)
                {
                    job.Start();
                }
            }
        }

        /// <summary>
        /// Pauses the selected backup job
        /// </summary>
        private void PauseSelectedJob()
        {
            if (SelectedJob != null)
                foreach (var job in SelectedJob)
                {
                    if (job.Status.State == BackupState.Running)
                    {
                        job.Pause();
                    }
                }
        }

        /// <summary>
        /// Determines if the selected job can be paused
        /// </summary>
        private bool CanPauseSelectedJob()
        {
            if (SelectedJob == null || SelectedJob.Count == 0)
                return false;
            foreach (var job in SelectedJob)
            {
                if (job.Status.State != BackupState.Running)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Resumes the selected backup job
        /// </summary>
        private void ResumeSelectedJob()
        {
            if (SelectedJob != null)
                foreach (var job in SelectedJob)
                {
                    if (job.Status.State == BackupState.Paused)
                    {
                        job.Resume();
                    }
                }
        }

        /// <summary>
        /// Determines if the selected job can be resumed
        /// </summary>
        private bool CanResumeSelectedJob()
        {
            if (SelectedJob == null || SelectedJob.Count == 0)
                return false;
            foreach (var job in SelectedJob)
            {
                if (job.Status.State != BackupState.Paused)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Launches multiple backup jobs that are selected in the UI
        /// </summary>
        private void LaunchMultipleJobs()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
                return;

            // Get indices of selected jobs
            var jobIndices = new List<int>();
            foreach (var job in SelectedJobs)
            {
                int index = Jobs.IndexOf(job);
                if (index >= 0)
                {
                    jobIndices.Add(index);
                }
            }

            // Execute selected jobs using BackupManager
            if (jobIndices.Count > 0)
            {
                var jobIndicesRef = jobIndices; // Create a reference variable
                _backupManager.ExecuteJobs(ref jobIndicesRef);
            }
        }

        /// <summary>
        /// Removes the selected backup job
        /// </summary>
        private void RemoveSelectedJob()
        {
            if (SelectedJob != null)
            {
                foreach (var job in SelectedJob)
                {
                    int index = Jobs.IndexOf(job);
                    if (index >= 0)
                    {
                        int indexRef = index; // Required for reference
                        if (_backupManager.RemoveJob(ref indexRef))
                        {
                            Jobs.Remove(job);
                            OnPropertyChanged(nameof(Jobs));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets a formatted string representation of job state
        /// </summary>
        public string GetJobStateDisplay(BackupJob job)
        {
            if (job == null)
                return string.Empty;

            return job.Status.State switch
            {
                BackupState.Waiting => "En attente",
                BackupState.Running => "En cours",
                BackupState.Paused => "En pause",
                BackupState.Completed => "Terminé",
                BackupState.Error => "Erreur",
                _ => "Inconnu"
            };
        }


        /// <summary>
        /// Gets the job progress as a string with percentage
        /// </summary>
        public string GetJobProgressDisplay(BackupJob job)
        {
            if (job == null)
                return "0%";

            return $"{job.Status.ProgressPercentage:F1}%";
        }

        /// <summary>
        /// Gets the job's remaining time estimation as a formatted string
        /// </summary>
        public string GetRemainingTimeDisplay(BackupJob job)
        {
            if (job == null || job.Status.State != BackupState.Running)
                return string.Empty;

            var remaining = job.Status.EstimatedTimeRemaining;

            if (remaining.TotalSeconds < 1)
                return "Calcul en cours...";

            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m {remaining.Seconds:D2}s";
            else if (remaining.TotalMinutes >= 1)
                return $"{remaining.Minutes}m {remaining.Seconds:D2}s";
            else
                return $"{remaining.Seconds}s";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Simple RelayCommand implementation for MVVM pattern
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}