using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.IO;

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

        private string currentTargetFIle;
        public string CurrentTargetFile
        {
            get
            {
                return currentTargetFIle;
            }
            set
            {
                if (currentTargetFIle != value)
                {
                    currentTargetFIle = value;
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
        public double ProgressPercentage => TotalSize > 0 ? Math.Round((double)(TotalSize - RemainingSize) / TotalSize * 100, 2) : 0;

        /// <summary>
        /// Transfer rate in bytes per second
        /// </summary>
        public double TransferRate => ElapsedTime.TotalSeconds > 0 ? TransferredSize / ElapsedTime.TotalSeconds : 0;

        /// <summary>
        /// Estimated time remaining
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
        /// Event manager for observer notifications
        /// </summary>
        public JobEventManager Events;


        private JobEventManager jobEventManager;
        private BackupJob backupJob;

        /// <summary>
        /// Backup job associated with this status
        /// </summary>
        public BackupJob BackupJob
        {
            get { return backupJob; }
            set
            {
                if (backupJob != value)
                {
                    backupJob = value;
                    OnPropertyChanged();
                }
            }
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
            // Initialize the event manager for observer notifications
            Events = new JobEventManager();
            ExecutionId = Guid.NewGuid();

            LoadStateFromPrevious(jobName);
        }

        /// <summary>
        /// Loads previous state from state.json file for a specific job
        /// </summary>
        /// <param name="jobName">Name of the job to load state for</param>
        /// <returns>True if a previous state was successfully loaded</returns>
        public bool LoadStateFromPrevious(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return false;

            try
            {
                // Get the state.json file path 
                string stateFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EasySave",
                    "state.json"
                );

                // Attempt to load the state from file
                var previousState = JobState.LoadFromStateFile(jobName, stateFilePath);
                if (previousState != null)
                {
                    // Apply the loaded state to this JobStatus
                    if (backupJob != null)
                    {
                        // If we already have a job reference, make sure the names match
                        if (!backupJob.Name.Equals(previousState.JobName, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }

                    // Set properties from the loaded state
                    TotalFiles = previousState.TotalFiles;
                    TotalSize = previousState.TotalSize;
                    RemainingFiles = previousState.RemainingFiles;
                    RemainingSize = previousState.TotalSize - (long)(previousState.TotalSize * previousState.ProgressPercentage / 100);

                    // Only initialize these if we actually have values
                    if (!string.IsNullOrEmpty(previousState.CurrentSourceFile))
                        CurrentSourceFile = previousState.CurrentSourceFile;

                    if (!string.IsNullOrEmpty(previousState.CurrentTargetFile))
                        CurrentTargetFile = previousState.CurrentTargetFile;

                    // Only use the previous state if it's not completed or error
                    if (previousState.State == BackupState.Running || previousState.State == BackupState.Paused)
                    {
                        // Set to waiting instead of the previous state to avoid automatic resumption
                        State = BackupState.Waiting;

                        // Import processed files for resuming capability
                        if (previousState.ProcessedFiles?.Count > 0)
                        {
                            foreach (var file in previousState.ProcessedFiles)
                            {
                                AddProcessedFile(file);
                            }
                        }

                        // Initialize time-related fields
                        StartTime = previousState.StartTime != DateTime.MinValue ? previousState.StartTime : DateTime.Now;

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading previous state for job {jobName}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Updates the job status and notifies observers
        /// </summary>
        public void Update()
        {
            // Notify observers of changes
            if (Events != null)
            {
                Events.NotifyListeners(this);
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
        public void Start()
        {
            StartTime = DateTime.Now;
            State = BackupState.Running;
            EndTime = null;
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
                ProcessedFiles = new List<string>(this.ProcessedFiles)
            };

            return snapshot;
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

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

}
