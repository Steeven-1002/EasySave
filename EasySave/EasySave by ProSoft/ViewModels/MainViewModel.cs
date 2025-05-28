using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// View model for managing backup jobs
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        private readonly IDialogService _dialogService;
        private ObservableCollection<BackupJob> _jobs;
        private DispatcherTimer _statusUpdateTimer;
        private volatile bool _isLaunchingJobs = false;

        // User notification properties
        public string NotificationMessage { get; private set; }
        public bool HasNotification => !string.IsNullOrEmpty(NotificationMessage);
        public event Action<string> ShowErrorMessage;
        public event Action<string> ShowInfoMessage;

        public ObservableCollection<BackupJob> Jobs
        {
            get => _jobs;
            set
            {
                _jobs = value;
                OnPropertyChanged();
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
        public ICommand RemoveJobCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }

        public MainViewModel(BackupManager backupManager) : this(backupManager, new DialogService()) { }

        public MainViewModel(BackupManager backupManager, IDialogService dialogService)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _jobs = new ObservableCollection<BackupJob>();

            Predicate<object?> canLaunchPredicate = _ =>
               SelectedJobs != null &&
               SelectedJobs.Any(job => job.Status.State == BackupState.Initialise ||
                                       job.Status.State == BackupState.Error ||
                                       job.Status.State == BackupState.Completed) &&
               !_isLaunchingJobs; // Do not allow launch if a launch is already in progress

            LaunchJobCommand = new RelayCommand(async _ => await LaunchSelectedJob(), canLaunchPredicate);

            CreateJobCommand = new RelayCommand(_ => CreateJob(), _ => true);
            RemoveJobCommand = new RelayCommand(_ => RemoveSelectedJob(), _ => SelectedJobs != null && SelectedJobs.Count > 0);
            PauseJobCommand = new RelayCommand(_ => PauseSelectedJob(), _ => CanPauseSelectedJob());
            ResumeJobCommand = new RelayCommand(_ => ResumeSelectedJob(), _ => CanResumeSelectedJob());
            StopJobCommand = new RelayCommand(_ => StopSelectedJobs(), _ => SelectedJobs != null && SelectedJobs.Any(job => job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused) && !_isLaunchingJobs);

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
            if (!_isLaunchingJobs)
                CommandManager.InvalidateRequerySuggested();
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

            OnPropertyChanged(nameof(Jobs));
            OnPropertyChanged(nameof(SelectedJobs));
        }

        /// <summary>
        /// Updates the selection based on checkbox states
        /// </summary>
        public void UpdateSelectionFromCheckboxes()
        {
            var selected = Jobs.Where(j => j.IsSelected).ToList();
            SelectedJobs = selected;
        }

        /// <summary>
        /// Adds a job to the observable collection and sets up property change notifications
        /// </summary>
        /// <param name="job">The job to add</param>
        public void JobAdded(BackupJob job)
        {
            if (!Jobs.Contains(job))
            {
                Jobs.Add(job);
                OnPropertyChanged(nameof(Jobs));
            }
        }

        /// <summary>
        /// Opens the job creation panel
        /// </summary>
        private void CreateJob()
        {
            Debug.WriteLine("MainViewModel.CreateJob() called.");
        }

        /// <summary>
        /// Launches the selected backup job
        /// </summary>
        private async Task LaunchSelectedJob()
        {
            // Checks if a launch is already in progress.
            if (_isLaunchingJobs)
            {
                Debug.WriteLine("LaunchSelectedJob: A launch is already in progress. Cancelling the new launch request.");
                return;
            }

            // Check if any jobs are selected.
            if (SelectedJobs == null || !SelectedJobs.Any())
            {
                Debug.WriteLine("LaunchSelectedJob: No job selected.");
                return;
            }

            // Filters which jobs can be launched (Initialize, Error, or Completed status).
            var jobsToStartModels = SelectedJobs.Where(j =>
                j.Status.State == BackupState.Initialise ||
                j.Status.State == BackupState.Error ||
                j.Status.State == BackupState.Completed).ToList();

            // Check if there are any jobs eligible for launch.
            if (!jobsToStartModels.Any())
            {
                Debug.WriteLine("LaunchSelectedJob: No suitable job to start based on current selection and status.");
                CommandManager.InvalidateRequerySuggested(); // Updates the state of the buttons.
                return;
            }

            _isLaunchingJobs = true; // Sets the indicator to show that a launch is in progress.
            CommandManager.InvalidateRequerySuggested(); // Disables launch buttons.
            Debug.WriteLine($"MainViewModel.LaunchSelectedJob: SET _isLaunchingJobs = true. Launching jobs: {string.Join(", ", jobsToStartModels.Select(j => j.Name))}");

            try
            {
                List<string> jobNamesToRun = new List<string>();
                foreach (var jobModel in jobsToStartModels)
                {
                    jobModel.Status.ResetForRun(); // Resets the job status for rerun.
                    Debug.WriteLine($"MainViewModel.LaunchSelectedJob: Job status '{jobModel.Name}' RESET for execution. New status: {jobModel.Status.State}");
                    jobNamesToRun.Add(jobModel.Name); // Adds the job name to the list of jobs to run.
                }

                if (jobNamesToRun.Any())
                {
                    await _backupManager.ExecuteJobsByNameAsync(jobNamesToRun);
                    Debug.WriteLine("MainViewModel.LaunchSelectedJob: BackupManager.ExecuteJobsByNameAsync awaited and completed.");
                }
                else
                {
                    Debug.WriteLine("MainViewModel.LaunchSelectedJob: No valid job names found to execute.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainViewModel.LaunchSelectedJob: EXCEPTION during job execution: {ex.Message}");
            }
            finally
            {
                _isLaunchingJobs = false; // Resets the indicator after all jobs are completed or in case of error.
                Debug.WriteLine("MainViewModel.LaunchSelectedJob: (in finally) SET _isLaunchingJobs = false.");
                CommandManager.InvalidateRequerySuggested(); // Reactivates the launch buttons.
            }
        }

        /// <summary>
        /// Pauses the selected backup job
        /// </summary>
        private void PauseSelectedJob()
        {
            if (SelectedJobs != null)
                foreach (var job in SelectedJobs)
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
        public bool CanPauseSelectedJob()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
                return false;

            return SelectedJobs.Any(job => job.Status.State == BackupState.Running);
        }

        /// <summary>
        /// Resumes the selected backup job
        /// </summary>
        private void ResumeSelectedJob()
        {
            if (SelectedJobs != null)
                foreach (var job in SelectedJobs)
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
        public bool CanResumeSelectedJob()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
                return false;

            return SelectedJobs.Any(job => job.Status.State == BackupState.Paused);
        }

        /// <summary>
        /// Removes the selected backup job
        /// </summary>
        private void RemoveSelectedJob()
        {
            var jobsToRemove = new List<BackupJob>(SelectedJobs);

            foreach (var job in jobsToRemove)
            {
                if (_backupManager.RemoveJobByName(job.Name))
                {
                    Jobs.Remove(job);
                }
            }
            UpdateSelectionFromCheckboxes();
            OnPropertyChanged(nameof(Jobs));
        }

        private void StopSelectedJobs()
        {
            if (SelectedJobs == null || _isLaunchingJobs) return; // Do not allow stopping if a launch is in progress (could be adjusted)
            Debug.WriteLine($"MainViewModel.StopSelectedJobs called for: {string.Join(", ", SelectedJobs.Where(j => j.Status.State == BackupState.Running || j.Status.State == BackupState.Paused).Select(j => j.Name))}");
            foreach (var job in SelectedJobs)
            {
                if (job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused) job.Stop();
            }
            CommandManager.InvalidateRequerySuggested();
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
                BackupState.Waiting => Localization.Resources.StateWaiting ?? "Waiting",
                BackupState.Running => Localization.Resources.StateRunning ?? "Running",
                BackupState.Paused => Localization.Resources.StatePaused ?? "Paused",
                BackupState.Completed => Localization.Resources.StateCompleted ?? "Completed",
                BackupState.Error => Localization.Resources.StateError ?? "Error",
                _ => Localization.Resources.StateUnknown ?? "Unknown"
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
                return Localization.Resources.Calculating ?? "Calculating...";

            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m {remaining.Seconds:D2}s";
            else if (remaining.TotalMinutes >= 1)
                return $"{remaining.Minutes}m {remaining.Seconds:D2}s";
            else
                return $"{remaining.Seconds}s";
        }

        /// <summary>
        /// Shows a notification message in the UI
        /// </summary>
        private void Notify(string message)
        {
            NotificationMessage = message;
            OnPropertyChanged(nameof(NotificationMessage));
            OnPropertyChanged(nameof(HasNotification));
        }

        /// <summary>
        /// Shows an error message
        /// </summary>
        private void NotifyError(string message)
        {
            _dialogService.ShowError(message);
        }

        /// <summary>
        /// Shows an information message
        /// </summary>
        public void NotifyInfo(string message)
        {
            _dialogService.ShowInformation(message);
        }

        /// <summary>
        /// Validates if jobs are selected, shows error if not
        /// </summary>
        public bool ValidateJobSelection()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
            {
                NotifyError("No backup selected");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Notifies about successfully launched jobs
        /// </summary>
        public void NotifyJobsLaunched(int count)
        {
            NotifyInfo($"{count} jobs have been launched.");
        }

        /// <summary>
        /// Notifies about job deletion
        /// </summary>
        public void NotifyJobDeleted()
        {
            NotifyInfo(Localization.Resources.MessageBoxDeleteJob);
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


        // This event is used to notify the UI to re-evaluate the CanExecute state of the command
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}