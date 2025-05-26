using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// View model for listing and managing backup jobs
    /// </summary>
    public class JobsListViewModel : INotifyPropertyChanged, JobEventListeners
    {
        private readonly BackupManager _backupManager;
        private readonly IDialogService _dialogService;
        private ObservableCollection<BackupJob> _jobs;
        private JobEventManager _jobEventManager = JobEventManager.Instance;
        private volatile bool _isLaunchingJobs = false;

        // User notification properties
        public event Action<string> ValidationError;
        public event Action<string> JobStatusChanged;

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

        private BackupState _status = BackupState.Initialise;
        public BackupState Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        private int _progressPercentage = 0;
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set { _progressPercentage = value; OnPropertyChanged(); }
        }

        public ICommand LaunchJobCommand { get; }
        public ICommand RemoveJobCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }
        public ICommand LaunchMultipleJobsCommand { get; }

        public JobsListViewModel(BackupManager backupManager) : this(backupManager, new DialogService()) { }

        public JobsListViewModel(BackupManager backupManager, IDialogService dialogService)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _jobs = new ObservableCollection<BackupJob>();

            // Register as a listener for job events
            _jobEventManager.AddListener(this);

            Predicate<object?> canLaunchPredicate = _ =>
               SelectedJobs != null &&
               SelectedJobs.Any(job => job.Status.State == BackupState.Initialise ||
                                       job.Status.State == BackupState.Error ||
                                       job.Status.State == BackupState.Completed) &&
               !_isLaunchingJobs;

            LaunchJobCommand = new RelayCommand(async _ => await LaunchSelectedJob(), canLaunchPredicate);
            RemoveJobCommand = new RelayCommand(_ => RemoveSelectedJob(), _ => SelectedJobs != null && SelectedJobs.Count > 0);
            PauseJobCommand = new RelayCommand(_ => PauseSelectedJob(), _ => CanPauseSelectedJob());
            ResumeJobCommand = new RelayCommand(_ => ResumeSelectedJob(), _ => CanResumeSelectedJob());
            StopJobCommand = new RelayCommand(_ => StopSelectedJobs(), _ => SelectedJobs != null && SelectedJobs.Any(job => job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused));
            LaunchMultipleJobsCommand = new RelayCommand(_ => LaunchMultipleJobs(), _ => CanLaunchMultipleJobs());

            // Load jobs initially
            LoadJobs();
        }

        public void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile, double transfertDuration, double encryptionTimeMs, string details = null)
        {
            // Update the job status based on the event data
            Status = newState;

            // Calculate progress percentage properly based on remaining vs total
            if (totalSize > 0)
            {
                ProgressPercentage = (int)Math.Round((double)(totalSize - remainingSize) / totalSize * 100, 0);
            }
            else if (totalFiles > 0)
            {
                ProgressPercentage = (int)Math.Round((double)(totalFiles - remainingFiles) / totalFiles * 100, 0);
            }
            else
            {
                ProgressPercentage = 100;
            }

            // Make sure to notify property changes
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(ProgressPercentage));
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
        /// Adds a job to the observable collection
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
                Debug.WriteLine("LaunchSelectedJob: No jobs selected.");
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
                Debug.WriteLine("LaunchSelectedJob: No appropriate jobs to start based on current selection and state.");
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            _isLaunchingJobs = true;
            CommandManager.InvalidateRequerySuggested();
            Debug.WriteLine($"JobsListViewModel.LaunchSelectedJob: SET _isLaunchingJobs = true. Launching jobs: {string.Join(", ", jobsToStartModels.Select(j => j.Name))}");

            try
            {
                List<string> jobNamesToRun = new List<string>();
                foreach (var jobModel in jobsToStartModels)
                {
                    jobModel.Status.ResetForRun();
                    Debug.WriteLine($"JobsListViewModel.LaunchSelectedJob: Job '{jobModel.Name}' status RESET for execution. New status: {jobModel.Status.State}");
                    jobNamesToRun.Add(jobModel.Name);
                }

                if (jobNamesToRun.Any())
                {
                    await _backupManager.ExecuteJobsByNameAsync(jobNamesToRun);
                    Debug.WriteLine("JobsListViewModel.LaunchSelectedJob: BackupManager.ExecuteJobsByNameAsync awaited and completed.");
                }
                else
                {
                    Debug.WriteLine("JobsListViewModel.LaunchSelectedJob: No valid job names found to execute.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JobsListViewModel.LaunchSelectedJob: EXCEPTION during job execution: {ex.Message}");
                ValidationError?.Invoke($"Error launching job: {ex.Message}");
            }
            finally
            {
                _isLaunchingJobs = false;
                Debug.WriteLine("JobsListViewModel.LaunchSelectedJob: (in finally) SET _isLaunchingJobs = false.");
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Launches multiple backup jobs
        /// </summary>
        private void LaunchMultipleJobs()
        {
            foreach (var job in SelectedJobs)
            {
                _backupManager.ExecuteJobsByNameAsync(new List<string> { job.Name }).Wait();
            }
        }

        /// <summary>
        /// Determines if multiple jobs can be launched
        /// </summary>
        private bool CanLaunchMultipleJobs()
        {
            return SelectedJobs != null && SelectedJobs.Count > 0;
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
                        JobStatusChanged?.Invoke($"Job '{job.Name}' has been paused.");
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
                        JobStatusChanged?.Invoke($"Job '{job.Name}' has been resumed.");
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
                    JobStatusChanged?.Invoke($"Job '{job.Name}' has been removed.");
                }
            }
            UpdateSelectionFromCheckboxes();
            OnPropertyChanged(nameof(Jobs));
        }

        /// <summary>
        /// Stops the selected backup jobs
        /// </summary>
        private void StopSelectedJobs()
        {
            if (SelectedJobs == null) return;
            Debug.WriteLine($"JobsListViewModel.StopSelectedJobs called for: {string.Join(", ", SelectedJobs.Where(j => j.Status.State == BackupState.Running || j.Status.State == BackupState.Paused).Select(j => j.Name))}");
            foreach (var job in SelectedJobs)
            {
                if (job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused)
                {
                    job.Stop();
                    JobStatusChanged?.Invoke($"Job '{job.Name}' has been stopped.");
                }
            }
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Validates if jobs are selected, shows error if not
        /// </summary>
        public bool ValidateJobSelection()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
            {
                ValidationError?.Invoke("No backup selected");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Notifies about job deletion
        /// </summary>
        public void NotifyJobDeleted()
        {
            JobStatusChanged?.Invoke(Localization.Resources.MessageBoxDeleteJob);
        }

        /// <summary>
        /// Notifies about jobs being launched
        /// </summary>
        public void NotifyJobsLaunched(int count)
        {
            JobStatusChanged?.Invoke($"{count} job(s) have been launched successfully.");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}