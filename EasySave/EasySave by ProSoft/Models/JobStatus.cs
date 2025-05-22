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
                if (TotalSize <= 0) return (State == BackupState.Completed && TotalFiles == 0) ? 100 : 0; // Cas o� il n'y a rien � sauvegarder mais le travail est "complet"
                if (State == BackupState.Completed) return 100; // Si complet, toujours 100%
                return Math.Round((double)TransferredSize / TotalSize * 100, 2);
            }
        }

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
        /// Additional details about job status (e.g. reason for stopping)
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Event manager for observer notifications
        /// </summary>
        public JobEventManager Events;

        private JobEventManager jobEventManager;
        private BackupJob? backupJob; // Rendre nullable

        public BackupJob? BackupJob // Rendre nullable
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
            // Votre logique existante, mais attention � ne pas �craser un �tat d�j� r�initialis�
            // ou un �tat en cours si cette m�thode est appel�e � des moments inopportuns.
            // Cette m�thode est plus pertinente si elle est appel�e une seule fois lors de l'initialisation de l'application.
            if (string.IsNullOrEmpty(jobName)) return false;
            try
            {
                string stateFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave", "state.json");

                var previousState = JobState.LoadFromStateFile(jobName, stateFilePath);
                if (previousState != null)
                {
                    // Appliquer l'�tat seulement si pertinent (par exemple, si on veut reprendre une t�che en pause)
                    if (previousState.State == BackupState.Paused) // Uniquement reprendre l'�tat si c'�tait en pause
                    {
                        TotalFiles = previousState.TotalFiles;
                        TotalSize = previousState.TotalSize;
                        RemainingFiles = previousState.RemainingFiles;
                        RemainingSize = previousState.RemainingSize; // Recalculer si ProgressPercentage est plus fiable
                        CurrentSourceFile = previousState.CurrentSourceFile ?? string.Empty;
                        CurrentTargetFile = previousState.CurrentTargetFile ?? string.Empty;
                        processedFiles = new List<string>(previousState.ProcessedFiles ?? new List<string>());
                        StartTime = previousState.StartTime != DateTime.MinValue ? previousState.StartTime : DateTime.Now; // Conserver l'heure de d�but originale
                        State = BackupState.Paused; // Remettre en pause pour que l'utilisateur puisse reprendre
                        Details = previousState.Details ?? string.Empty;
                        Update(); // Notifier les changements
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading state from file for {jobName}: {ex.Message}");
                // System.Windows.Forms.MessageBox.Show($"Error loading state from file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                ProcessedFiles = new List<string>(this.ProcessedFiles),
                Details = this.Details ?? string.Empty
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

        public void ResetForRun()
        {
            State = BackupState.Initialise;
            TotalFiles = 0;
            TotalSize = 0;
            RemainingFiles = 0;
            RemainingSize = 0;
            // ProgressPercentage sera recalcul�
            CurrentSourceFile = string.Empty;
            CurrentTargetFile = string.Empty;
            Details = string.Empty;
            ErrorMessage = string.Empty;
            EncryptionTimeMs = 0;
            StartTime = DateTime.MinValue; // Indique que le travail n'a pas encore r�ellement d�marr� pour cette ex�cution
            EndTime = null;
            processedFiles.Clear(); // Vider la liste des fichiers trait�s pour la nouvelle ex�cution
            ExecutionId = Guid.NewGuid(); // Un nouvel ID pour cette nouvelle tentative d'ex�cution

            System.Diagnostics.Debug.WriteLine($"JobStatus for '{BackupJob?.Name ?? "Unknown Job"}' has been RESET for run.");
            Update(); // Notifier l'UI et les listeners que l'�tat a �t� r�initialis�
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
