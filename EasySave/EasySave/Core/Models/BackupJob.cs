using EasySave.Interfaces;

namespace EasySave.Core.Models
{
    /// <summary>
    /// Represents a backup job with its configuration, state, and execution logic.
    /// </summary>
    public class BackupJob
    {
        /// <summary>
        /// Gets or sets the name of the backup job.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the source path for the backup job.
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// Gets or sets the target path for the backup job.
        /// </summary>
        public string TargetPath { get; set; }

        /// <summary>
        /// Gets or sets the type of the backup (e.g., Full or Differential).
        /// </summary>
        public BackupType Type { get; set; }

        /// <summary>
        /// Gets or sets the current state of the backup job.
        /// </summary>
        public BackupState State { get; set; }

        /// <summary>
        /// Gets or sets the last execution time of the backup job.
        /// </summary>
        public DateTime LastRunTime { get; set; }

        /// <summary>
        /// Gets the creation time of the backup job.
        /// </summary>
        public DateTime CreationTime { get; init; }

        /// <summary>
        /// Gets or sets the backup strategy used for this job.
        /// </summary>
        public IBackupStrategy Strategy { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BackupJob"/> class.
        /// </summary>
        /// <param name="name">The name of the backup job.</param>
        /// <param name="sourcePath">The source path for the backup.</param>
        /// <param name="targetPath">The target path for the backup.</param>
        /// <param name="type">The type of the backup (Full or Differential).</param>
        /// <param name="strategy">The backup strategy to use.</param>
        public BackupJob(string name, string sourcePath, string targetPath, BackupType type, IBackupStrategy strategy)
        {
            Name = name;
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Type = type;
            Strategy = strategy;
            State = BackupState.INACTIVE;
            CreationTime = DateTime.Now;
            LastRunTime = DateTime.MinValue;
            UpdateState();
        }

        /// <summary>
        /// Executes the backup job using the specified strategy.
        /// </summary>
        public void Execute()
        {
            Console.WriteLine($"BackupJob '{Name}': Preparing to execute via strategy '{Strategy.GetType().Name}'.");
            this.State = BackupState.ACTIVE;
            Strategy.RegisterObserver(Services.LoggingBackup.Instance);
            Strategy.RegisterStateObserver(StateManager.Instance);
            Strategy.Execute(this);
            this.LastRunTime = DateTime.Now;
        }

        /// <summary>
        /// Retrieves the list of files to be backed up for this job.
        /// </summary>
        /// <returns>A list of file paths to be backed up.</returns>
        public List<string> GetFilesToBackup()
        {
            return Strategy.GetFilesToBackup(this);
        }

        /// <summary>
        /// Updates the state of the backup job by loading the current state from the state manager.
        /// </summary>
        private void UpdateState()
        {
            StateManager.Instance.LoadState();
            var jobState = StateManager.Instance.GetState(Name);
            if (jobState != null)
            {
                if (Enum.TryParse(typeof(BackupState), jobState.State, out var parsedState))
                {
                    State = (BackupState)parsedState;
                }
                else
                {
                    State = BackupState.INACTIVE; // Default or fallback state
                }
            }
        }

        /// <summary>
        /// Returns a string representation of the backup job.
        /// </summary>
        /// <returns>A string containing the job's details.</returns>
        public override string ToString()
        {
            return $"Job: {Name}, Type: {Type}, Source: {SourcePath}, Target: {TargetPath}, State: {State}, LastRun: {LastRunTime}";
        }
    }
}