using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Provides comprehensive job tracking capabilities throughout the backup process lifecycle
    /// </summary>
    public class JobStatus : INotifyPropertyChanged
    {
        private long totalSize;
        public long TotalSize
        {
            get
            {
                return totalSize;
            }
            set
            {
                if (totalSize != value)
                {
                    totalSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private int totalFiles;
        public int TotalFiles
        {
            get
            {
                return totalFiles;
            }
            set
            {
                if (totalFiles != value)
                {
                    totalFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        private BackupState state;
        public BackupState State
        {
            get
            {
                return state;
            }
            set
            {
                if (state != value)
                {
                    state = value;
                    LastStateChangeTime = DateTime.Now;
                    OnPropertyChanged();
                }
            }
        }

        private long remainingSize;
        public long RemainingSize
        {
            get
            {
                return remainingSize;
            }
            set
            {
                if (remainingSize != value)
                {
                    remainingSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(TransferredSize));
                }
            }
        }

        private int remainingFiles;
        public int RemainingFiles
        {
            get
            {
                return remainingFiles;
            }
            set
            {
                if (remainingFiles != value)
                {
                    remainingFiles = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProcessedFiles));
                }
            }
        }

        private string currentTargetFile;
        public string CurrentTargetFile
        {
            get
            {
                return currentTargetFile;
            }
            set
            {
                if (currentTargetFile != value)
                {
                    currentTargetFile = value;
                    OnPropertyChanged();
                }
            }
        }

        private string currentSourceFile;
        public string CurrentSourceFile
        {
            get
            {
                return currentSourceFile;
            }
            set
            {
                if (currentSourceFile != value)
                {
                    currentSourceFile = value;
                    OnPropertyChanged();
                }
            }
        }

        // Properties for job tracking

        /// <summary>
        /// Restoration points for backup resume
        /// </summary>
        private List<string> processedFiles = new List<string>();
        public List<string> ProcessedFiles => processedFiles;

        /// <summary>
        /// Job start time
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// Job end time
        /// </summary>
        public DateTime? EndTime { get; private set; }

        /// <summary>
        /// Time elapsed since job start
        /// </summary>
        public TimeSpan ElapsedTime => (EndTime ?? DateTime.Now) - StartTime;

        /// <summary>
        /// Last state update timestamp
        /// </summary>
        public DateTime LastStateChangeTime { get; private set; }

        /// <summary>
        /// Unique execution ID for this job run
        /// </summary>
        public Guid ExecutionId { get; private set; }

        /// <summary>
        /// Total size of transferred data
        /// </summary>
        public long TransferredSize => TotalSize - RemainingSize;

        /// <summary>
        /// Progress percentage
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                if (TotalSize <= 0) return (State == BackupState.Completed && TotalFiles == 0) ? 100 : 0; // Case where there is nothing to save but the job is "complete"
                if (State == BackupState.Completed) return 100;
                return Math.Round((double)TransferredSize / TotalSize * 100, 2);
            }
        }

        /// <summary>
        /// Transfer rate in bytes per second
        /// </summary>
        public double TransferRate => ElapsedTime.TotalSeconds > 0 ? TransferredSize / ElapsedTime.TotalSeconds : 0;

        /// <summary>
        /// Time remaining until job completion based on current transfer rate
        /// </summary>
        public TimeSpan EstimatedTimeRemaining
        {
            get
            {
                if (TransferRate > 0 && RemainingSize > 0)
                    return TimeSpan.FromSeconds(RemainingSize / TransferRate);
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Error messages if applicable
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Additional details about job status (e.g. reason for stopping)
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Event manager for observer notifications
        /// </summary>
        public JobEventManager Events;

        private JobEventManager jobEventManager;
        private BackupJob? backupJob; // Make nullable

        public BackupJob? BackupJob // Make nullable
        {
            get => backupJob;
            set { if (backupJob != value) { backupJob = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Optionally, implement this interface for logging encryption time
        /// </summary>
        public long EncryptionTimeMs { get; set; }

        /// <summary>
        /// JobStatus constructor that initializes from a saved state
        /// </summary>
        /// <param name="jobName">Name of the job to load state for</param>
        public JobStatus(string jobName)
        {
            Events = JobEventManager.Instance;
            this.state = BackupState.Initialise;
            this.totalFiles = 0;
            this.totalSize = 0;
            this.remainingFiles = 0;
            this.remainingSize = 0;
            this.EncryptionTimeMs = 0;
            this.StartTime = DateTime.MinValue;
            this.LastStateChangeTime = DateTime.Now;
            this.ExecutionId = Guid.NewGuid();
        }

        /// <summary>
        /// Loads previous state from state.json file for a specific job
        /// </summary>
        /// <param name="jobName">Name of the job to load state for</param>
        /// <returns>True if a previous state was successfully loaded</returns>
        public bool LoadStateFromPrevious(string jobName)
        {
            if (string.IsNullOrEmpty(jobName)) return false;
            try
            {
                string stateFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave", "state.json");

                var previousState = JobState.LoadFromStateFile(jobName, stateFilePath);
                if (previousState != null)
                {
                    if (previousState.State == BackupState.Paused) // Only resume state if it was paused
                    {
                        TotalFiles = previousState.TotalFiles;
                        TotalSize = previousState.TotalSize;
                        RemainingFiles = previousState.RemainingFiles;
                        RemainingSize = previousState.RemainingSize;
                        CurrentSourceFile = previousState.CurrentSourceFile ?? string.Empty;
                        CurrentTargetFile = previousState.CurrentTargetFile ?? string.Empty;
                        processedFiles = previousState.ProcessedFiles != null
                            ? new List<string>(previousState.ProcessedFiles)
                            : new List<string>();
                        StartTime = previousState.StartTime != DateTime.MinValue ? previousState.StartTime : DateTime.Now;
                        State = BackupState.Paused;
                        Details = previousState.Details ?? string.Empty;
                        Update();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading state from file for {jobName}: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Updates the job status and notifies observers
        /// </summary>
        public void Update()
        {
            try
            {
                // Notify observers of changes
                if (Events != null)
                {
                    Events.NotifyListeners(this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in JobStatus.Update: {ex.Message}");
            }
        }

        /// <summary>
        /// Marks a file as processed to enable resume functionality
        /// </summary>
        /// <param name="filePath">Path of the processed file</param>
        public void AddProcessedFile(string filePath)
        {
            if (!processedFiles.Contains(filePath))
            {
                processedFiles.Add(filePath);
            }
        }

        /// <summary>
        /// Indicates that the job has started
        /// </summary>
        /// <param name="details">Optional details about the job start</param>
        public void Start(string details = null)
        {
            StartTime = DateTime.Now;
            State = BackupState.Running;
            EndTime = null;
            if (!string.IsNullOrEmpty(details))
            {
                Details = details;
            }
            Update();
        }

        /// <summary>
        /// Indicates that the job is paused
        /// </summary>
        public void Pause()
        {
            State = BackupState.Paused;
            Update();
        }

        /// <summary>
        /// Indicates that the job is completed
        /// </summary>
        public void Complete()
        {
            State = BackupState.Completed;
            EndTime = DateTime.Now;
            RemainingFiles = 0;
            RemainingSize = 0;
            Update();
        }

        /// <summary>
        /// Indicates that the job encountered an error
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        public void SetError(string errorMessage)
        {
            State = BackupState.Error;
            ErrorMessage = errorMessage;
            EndTime = DateTime.Now;
            Update();
        }

        /// <summary>
        /// Creates a snapshot of current state for persistence or display
        /// </summary>
        /// <returns>Job state for serialization</returns>
        public JobState CreateSnapshot()
        {
            try
            {
                var snapshot = new JobState
                {
                    JobName = backupJob?.Name ?? string.Empty,
                    SourcePath = backupJob?.SourcePath ?? string.Empty,
                    TargetPath = backupJob?.TargetPath ?? string.Empty,
                    Type = backupJob?.Type ?? BackupType.Full,
                    Timestamp = DateTime.Now,
                    State = this.State,
                    TotalFiles = this.TotalFiles,
                    TotalSize = this.TotalSize,
                    RemainingFiles = this.RemainingFiles,
                    RemainingSize = this.RemainingSize,
                    CurrentSourceFile = this.CurrentSourceFile ?? string.Empty,
                    CurrentTargetFile = this.CurrentTargetFile ?? string.Empty,
                    StartTime = this.StartTime,
                    EndTime = this.EndTime,
                    TransferRate = this.TransferRate,
                    ProgressPercentage = this.ProgressPercentage,
                    ExecutionId = this.ExecutionId,
                    EncryptionTimeMs = this.EncryptionTimeMs,
                    ProcessedFiles = this.ProcessedFiles != null ? new List<string>(this.ProcessedFiles) : new List<string>(),
                    Details = this.Details ?? string.Empty
                };

                System.Diagnostics.Debug.WriteLine($"Created snapshot for job: {snapshot.JobName}, State: {snapshot.State}, Progress: {snapshot.ProgressPercentage}%");
                return snapshot;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating job state snapshot: {ex.Message}");

                // Return a minimal valid snapshot to avoid null reference exceptions
                return new JobState
                {
                    JobName = backupJob?.Name ?? "Error",
                    State = BackupState.Error,
                    Timestamp = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Resumes job execution after pause or stop
        /// </summary>
        public void Resume()
        {
            if (State == BackupState.Paused)
            {
                State = BackupState.Running;
                Update();
            }
        }

        public void ResetForRun()
        {
            State = BackupState.Initialise;
            TotalFiles = 0;
            TotalSize = 0;
            RemainingFiles = 0;
            RemainingSize = 0;
            CurrentSourceFile = string.Empty;
            CurrentTargetFile = string.Empty;
            Details = string.Empty;
            ErrorMessage = string.Empty;
            EncryptionTimeMs = 0;
            StartTime = DateTime.MinValue; // Indicates that the job has not actually started yet for this run
            EndTime = null;
            processedFiles.Clear(); // Clear the list of processed files for the new run
            ExecutionId = Guid.NewGuid(); // A new ID for this new execution attempt

            System.Diagnostics.Debug.WriteLine($"JobStatus for '{BackupJob?.Name ?? "Unknown Job"}' has been RESET for run.");
            Update(); // Notify the UI and listeners that the state has been reset
        }
        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

}
