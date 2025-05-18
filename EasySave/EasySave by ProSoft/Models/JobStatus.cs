using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySave_by_ProSoft.Models {
    /// <summary>
    /// Provides comprehensive job tracking capabilities throughout the backup process lifecycle
    /// </summary>
    public class JobStatus : INotifyPropertyChanged {
        private long totalSize;
        public long TotalSize {
            get {
                return totalSize;
            }
            set {
                if (totalSize != value) {
                    totalSize = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private int totalFiles;
        public int TotalFiles {
            get {
                return totalFiles;
            }
            set {
                if (totalFiles != value) {
                    totalFiles = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private BackupState state;
        public BackupState State {
            get {
                return state;
            }
            set {
                if (state != value) {
                    state = value;
                    LastStateChangeTime = DateTime.Now;
                    OnPropertyChanged();
                }
            }
        }
        
        private long remainingSize;
        public long RemainingSize {
            get {
                return remainingSize;
            }
            set {
                if (remainingSize != value) {
                    remainingSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(TransferredSize));
                }
            }
        }
        
        private int remainingFiles;
        public int RemainingFiles {
            get {
                return remainingFiles;
            }
            set {
                if (remainingFiles != value) {
                    remainingFiles = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProcessedFiles));
                }
            }
        }
        
        private string currentTargetFIle;
        public string CurrentTargetFIle {
            get {
                return currentTargetFIle;
            }
            set {
                if (currentTargetFIle != value) {
                    currentTargetFIle = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private string currentSourceFile;
        public string CurrentSourceFile {
            get {
                return currentSourceFile;
            }
            set {
                if (currentSourceFile != value) {
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
        public TimeSpan EstimatedTimeRemaining {
            get {
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
        
        private BackupState backupState;
        private JobEventManager jobEventManager;
        private BackupJob backupJob;
        
        /// <summary>
        /// Optionally, implement this interface for logging encryption time
        /// </summary>
        public long EncryptionTimeMs { get; set; }
        
        /// <summary>
        /// JobStatus constructor
        /// </summary>
        public JobStatus() {
            Events = new JobEventManager();
            ExecutionId = Guid.NewGuid();
            StartTime = DateTime.Now;
            State = BackupState.Waiting;
            LastStateChangeTime = StartTime;
        }
        
        /// <summary>
        /// Updates the job status and notifies observers
        /// </summary>
        public void Update()
        {
            // Notify observers of changes
            if (Events != null)
            {
                var jobStatusCopy = this; // Create a local copy of the object
                Events.NotifyListeners(ref jobStatusCopy); // Pass local copy by reference
            }
        }
        
        /// <summary>
        /// Marks a file as processed to enable resume functionality
        /// </summary>
        /// <param name="filePath">Path of the processed file</param>
        public void AddProcessedFile(string filePath) {
            if (!processedFiles.Contains(filePath)) {
                processedFiles.Add(filePath);
            }
        }
        
        /// <summary>
        /// Indicates that the job has started
        /// </summary>
        public void Start() {
            StartTime = DateTime.Now;
            State = BackupState.Running;
            EndTime = null;
            Update();
        }
        
        /// <summary>
        /// Indicates that the job is paused
        /// </summary>
        public void Pause() {
            State = BackupState.Paused;
            Update();
        }
        
        /// <summary>
        /// Indicates that the job is completed
        /// </summary>
        public void Complete() {
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
        public void SetError(string errorMessage) {
            State = BackupState.Error;
            ErrorMessage = errorMessage;
            EndTime = DateTime.Now;
            Update();
        }
        
        /// <summary>
        /// Creates a snapshot of current state for persistence or display
        /// </summary>
        /// <returns>Job state for serialization</returns>
        public JobState CreateSnapshot() {
            var snapshot = new JobState {
                JobName = backupJob?.Name ?? string.Empty,
                Timestamp = DateTime.Now,
                State = this.State,
                TotalFiles = this.TotalFiles,
                TotalSize = this.TotalSize,
                CurrentSourceFile = this.CurrentSourceFile,
                CurrentTargetFile = this.CurrentTargetFIle
            };
            
            return snapshot;
        }
        
        /// <summary>
        /// Resumes job execution after pause or stop
        /// </summary>
        public void Resume() {
            if (State == BackupState.Paused) {
                State = BackupState.Running;
                Update();
            }
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
