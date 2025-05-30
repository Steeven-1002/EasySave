using EasySave_by_ProSoft.Core;
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
    public class JobsListViewModel : INotifyPropertyChanged, JobEventListeners, IEventListener, IDisposable
    {
        private readonly BackupManager _backupManager;
        private readonly IDialogService _dialogService;
        private ObservableCollection<BackupJob> _jobs;
        private JobEventManager _jobEventManager = JobEventManager.Instance;
        private EventManager _eventManager = EventManager.Instance;
        private volatile bool _isLaunchingJobs = false;
        private bool _disposed = false;

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
            Status = BackupState.Initialise;
            SelectedJobs = new ObservableCollection<BackupJob>().ToList();
            // Register as a listener for job events
            _jobEventManager.AddListener(this);

            // Register as a listener for remote control events
            _eventManager.AddListener(this);

            // Load initially and set up command
            LoadJobs();

            LaunchJobCommand = new RelayCommand(async _ => await LaunchSelectedJob(), _ => CanLaunchJob());
            RemoveJobCommand = new RelayCommand(_ => RemoveSelectedJob(SelectedJobs), _ => SelectedJobs != null && SelectedJobs.Count > 0);

            // Update commands to work with parameter (the job) if provided, otherwise use selected jobs
            PauseJobCommand = new RelayCommand(job => PauseJob(job), _ => true);
            ResumeJobCommand = new RelayCommand(job => ResumeJob(job), _ => true);
            StopJobCommand = new RelayCommand(job => StopJob(job), _ => true);
            LaunchMultipleJobsCommand = new RelayCommand(_ => LaunchMultipleJobs(), _ => CanLaunchMultipleJobs());
        }

        private bool CanLaunchJob()
        {
            return SelectedJobs != null &&
                   SelectedJobs.Any(job => job.Status.State == BackupState.Initialise ||
                                          job.Status.State == BackupState.Error ||
                                          job.Status.State == BackupState.Completed) &&
                   !_isLaunchingJobs;
        }

        private bool CanStopSelectedJob()
        {
            return SelectedJobs != null && SelectedJobs.Any(job => job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused);
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
            // Check if any jobs are selected.
            if (SelectedJobs == null || !SelectedJobs.Any())
            {
                Debug.WriteLine("LaunchSelectedJob: No jobs selected.");
                return;
            }

            // Check if business software is running
            string appNameToMonitor = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
            if (!string.IsNullOrEmpty(appNameToMonitor) &&
                new BusinessApplicationMonitor(appNameToMonitor).IsRunning())
            {
                Debug.WriteLine("LaunchSelectedJob: Business software is running, cannot launch jobs.");
                ValidationError?.Invoke("Cannot launch jobs while business software is running");
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
        /// Pauses a job or selected jobs if no specific job is provided
        /// </summary>
        private void PauseJob(object jobParameter)
        {
            // If a specific job is provided, pause it
            if (jobParameter is BackupJob specificJob)
            {
                if (specificJob.Status.State == BackupState.Running)
                {
                    specificJob.Pause();
                    Debug.WriteLine($"JobsListViewModel.PauseJob: Paused specific job '{specificJob.Name}'");
                    CommandManager.InvalidateRequerySuggested(); // Update command states
                }
                return;
            }

            // Otherwise, pause all selected jobs
            if (SelectedJobs != null && SelectedJobs.Count > 0)
            {
                foreach (var job in SelectedJobs)
                {
                    if (job.Status.State == BackupState.Running)
                    {
                        job.Pause();
                        Debug.WriteLine($"JobsListViewModel.PauseJob: Paused selected job '{job.Name}'");
                    }
                }
                CommandManager.InvalidateRequerySuggested(); // Update command states
            }
            else
            {
                // If no job is selected, show a message
                _dialogService.ShowInformation("Please select a job to pause or click directly on a job's pause button.", "Information");
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
        /// Resumes a job or selected jobs if no specific job is provided
        /// </summary>
        private void ResumeJob(object jobParameter)
        {
            // Check if business software is running
            string appNameToMonitor = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
            if (!string.IsNullOrEmpty(appNameToMonitor) &&
                new BusinessApplicationMonitor(appNameToMonitor).IsRunning())
            {
                Debug.WriteLine("ResumeJob: Business software is running, cannot resume jobs.");
                _dialogService.ShowInformation("Cannot resume jobs while business software is running.", "Business Software Running");
                return;
            }

            // If a specific job is provided, resume it
            if (jobParameter is BackupJob specificJob)
            {
                if (specificJob.Status.State == BackupState.Paused)
                {
                    specificJob.Resume();
                    Debug.WriteLine($"JobsListViewModel.ResumeJob: Resumed specific job '{specificJob.Name}'");
                    CommandManager.InvalidateRequerySuggested(); // Update command states
                }
                // Add ability to start jobs in Initialise, Error, or Completed states
                else if (specificJob.Status.State == BackupState.Initialise ||
                         specificJob.Status.State == BackupState.Error ||
                         specificJob.Status.State == BackupState.Completed)
                {
                    LaunchJob(specificJob);
                }
                return;
            }

            // Otherwise, resume all selected jobs
            if (SelectedJobs != null && SelectedJobs.Count > 0)
            {
                foreach (var job in SelectedJobs)
                {
                    if (job.Status.State == BackupState.Paused)
                    {
                        job.Resume();
                        Debug.WriteLine($"JobsListViewModel.ResumeJob: Resumed selected job '{job.Name}'");
                    }
                    // Add ability to start jobs in Initialise, Error, or Completed states
                    else if (job.Status.State == BackupState.Initialise ||
                             job.Status.State == BackupState.Error ||
                             job.Status.State == BackupState.Completed)
                    {
                        LaunchJob(job);
                    }
                }
                CommandManager.InvalidateRequerySuggested(); // Update command states
            }
            else
            {
                // If no job is selected, show a message
                _dialogService.ShowInformation("Please select a job to resume or click directly on a job's resume button.", "Information");
            }
        }

        /// <summary>
        /// Launches a specific job
        /// </summary>
        private async void LaunchJob(BackupJob job)
        {
            if (job != null)
            {
                // Check if business software is running
                string appNameToMonitor = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
                if (!string.IsNullOrEmpty(appNameToMonitor) &&
                    new BusinessApplicationMonitor(appNameToMonitor).IsRunning())
                {
                    Debug.WriteLine("LaunchJob: Business software is running, cannot launch job.");
                    ValidationError?.Invoke("Cannot launch job while business software is running");
                    return;
                }

                job.Status.ResetForRun();
                Debug.WriteLine($"JobsListViewModel.LaunchJob: Reset job '{job.Name}' status for execution. New status: {job.Status.State}");

                List<string> jobNames = new List<string> { job.Name };
                _isLaunchingJobs = true;
                CommandManager.InvalidateRequerySuggested();

                try
                {
                    Debug.WriteLine($"JobsListViewModel.LaunchJob: Starting execution of job '{job.Name}'");
                    await _backupManager.ExecuteJobsByNameAsync(jobNames);
                    Debug.WriteLine($"JobsListViewModel.LaunchJob: Job '{job.Name}' execution completed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"JobsListViewModel.LaunchJob: Error executing job '{job.Name}': {ex.Message}");
                    job.Status.SetError($"Error: {ex.Message}");
                }
                finally
                {
                    _isLaunchingJobs = false;
                    CommandManager.InvalidateRequerySuggested();
                    Debug.WriteLine($"JobsListViewModel.LaunchJob: (in finally) SET _isLaunchingJobs = false for job '{job.Name}'");
                }
            }
        }

        /// <summary>
        /// Determines if the selected job can be resumed or started
        /// </summary>
        public bool CanResumeSelectedJob()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
                return false;

            return SelectedJobs.Any(job => job.Status.State == BackupState.Paused ||
                                          job.Status.State == BackupState.Initialise ||
                                          job.Status.State == BackupState.Error ||
                                          job.Status.State == BackupState.Completed);
        }

        /// <summary>
        /// Removes the selected backup job
        /// </summary>
        public void RemoveSelectedJob(List<BackupJob> SelectedJobs)
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

        /// <summary>
        /// Stops a job or selected jobs if no specific job is provided
        /// </summary>
        private void StopJob(object jobParameter)
        {
            // If a specific job is provided, stop it
            if (jobParameter is BackupJob specificJob)
            {
                if (specificJob.Status.State == BackupState.Running || specificJob.Status.State == BackupState.Paused)
                {
                    specificJob.Stop();
                    Debug.WriteLine($"JobsListViewModel.StopJob: Stopped specific job '{specificJob.Name}'");
                    CommandManager.InvalidateRequerySuggested(); // Update command states
                }
                return;
            }

            // Otherwise, stop all selected jobs
            if (SelectedJobs != null && SelectedJobs.Count > 0)
            {
                foreach (var job in SelectedJobs)
                {
                    if (job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused)
                    {
                        job.Stop();
                        Debug.WriteLine($"JobsListViewModel.StopJob: Stopped selected job '{job.Name}'");
                    }
                }
                CommandManager.InvalidateRequerySuggested(); // Update command states
            }
            else
            {
                // If no job is selected, show a message
                _dialogService.ShowInformation("Please select a job to stop or click directly on a job's stop button.", "Information");
            }
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

        // IEventListener implementation methods
        public void OnJobStatusChanged(JobStatus status)
        {
            // Update the UI when job status changes
            if (status != null)
            {
                Debug.WriteLine($"JobsListViewModel.OnJobStatusChanged: Job '{status.BackupJob?.Name}' state changed to {status.State}");

                // Use the dispatcher to ensure we're on the UI thread
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => RefreshJobInList(status));
                }
                else
                {
                    RefreshJobInList(status);
                }
            }
        }

        /// <summary>
        /// Refreshes a specific job in the list when its status changes
        /// </summary>
        private void RefreshJobInList(JobStatus status)
        {
            if (status?.BackupJob == null)
                return;

            // Find the job in the list
            var job = Jobs.FirstOrDefault(j => j.Name == status.BackupJob.Name);
            if (job != null)
            {
                // The job exists in our list, refresh its properties
                // This will trigger UI updates through property change notifications
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(Jobs));
            }
            else
            {
                // Job not found in our list, reload all jobs
                LoadJobs();
            }
        }

        public void OnBusinessSoftwareStateChanged(bool isRunning)
        {
            Debug.WriteLine($"JobsListViewModel.OnBusinessSoftwareStateChanged: Business software running state is now {isRunning}");

            // Use the dispatcher to update UI
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => UpdateUIForBusinessSoftwareState(isRunning));
            }
            else
            {
                UpdateUIForBusinessSoftwareState(isRunning);
            }
        }

        /// <summary>
        /// Updates the UI based on business software state changes
        /// </summary>
        private void UpdateUIForBusinessSoftwareState(bool isRunning)
        {
            if (isRunning)
            {
                // Show notification to user
                JobStatusChanged?.Invoke("Business software has started. Running jobs will be paused.");
            }
            else
            {
                // Show notification to user
                JobStatusChanged?.Invoke("Business software has stopped. Jobs paused by business software will be resumed.");
            }

            // Force command state update
            CommandManager.InvalidateRequerySuggested();
        }

        public void OnLaunchJobsRequested(List<string> jobNames)
        {
            // When jobs are launched remotely, refresh the local job list to show updated status
            Debug.WriteLine($"JobsListViewModel.OnLaunchJobsRequested: Jobs launched remotely: {string.Join(", ", jobNames)}");

            // Use the dispatcher to ensure we're on the UI thread
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LoadJobs());
            }
            else
            {
                LoadJobs();
            }
        }

        public void OnPauseJobsRequested(List<string> jobNames)
        {
            // When jobs are paused remotely, refresh the local job list
            Debug.WriteLine($"JobsListViewModel.OnPauseJobsRequested: Jobs paused remotely: {string.Join(", ", jobNames)}");

            // Use the dispatcher to ensure we're on the UI thread
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LoadJobs());
            }
            else
            {
                LoadJobs();
            }
        }

        public void OnResumeJobsRequested(List<string> jobNames)
        {
            // When jobs are resumed remotely, refresh the local job list
            Debug.WriteLine($"JobsListViewModel.OnResumeJobsRequested: Jobs resumed remotely: {string.Join(", ", jobNames)}");

            // Use the dispatcher to ensure we're on the UI thread
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LoadJobs());
            }
            else
            {
                LoadJobs();
            }
        }

        public void OnStopJobsRequested(List<string> jobNames)
        {
            // When jobs are stopped remotely, refresh the local job list
            Debug.WriteLine($"JobsListViewModel.OnStopJobsRequested: Jobs stopped remotely: {string.Join(", ", jobNames)}");

            // Use the dispatcher to ensure we're on the UI thread
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LoadJobs());
            }
            else
            {
                LoadJobs();
            }
        }

        // IDisposable implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unregister from event managers
                    _jobEventManager.RemoveListener(this);
                    _eventManager.RemoveListener(this);
                    Debug.WriteLine("JobsListViewModel: Unregistered from event managers");
                }

                _disposed = true;
            }
        }

        ~JobsListViewModel()
        {
            Dispose(false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}