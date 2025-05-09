using EasySave.Models;

namespace EasySave
{
    /// <summary>
    /// Represents the type of backup that can be performed.
    /// </summary>
    public enum BackupType
    {
        /// <summary>
        /// A full backup, which copies all files regardless of previous backups.
        /// </summary>
        FULL = 0,

        /// <summary>
        /// A differential backup, which copies only files that have changed since the last full backup.
        /// </summary>
        DIFFERENTIAL = 1
    }
    /// <summary>
    /// Represents the state of a backup job, including its progress and current activity.
    /// </summary>
    public class JobState
    {
        /// <summary>
        /// Gets or sets the name of the backup job.
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the current state.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the current state of the backup job (e.g., active, inactive).
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Gets or sets the total number of files to be processed in the backup job.
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Gets or sets the total size of all files to be processed in the backup job, in bytes.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Gets or sets the number of files remaining to be processed in the backup job.
        /// </summary>
        public int RemainingFiles { get; set; }

        /// <summary>
        /// Gets or sets the total size of files remaining to be processed in the backup job, in bytes.
        /// </summary>
        public long RemainingSize { get; set; }

        /// <summary>
        /// Gets or sets the path of the current source file being processed.
        /// </summary>
        public string CurrentSourceFile { get; set; }

        /// <summary>
        /// Gets or sets the path of the current target file being processed.
        /// </summary>
        public string CurrentTargetFile { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JobState"/> class with the specified job name.
        /// </summary>
        /// <param name="jobName">The name of the backup job.</param>
        public JobState(string jobName)
        {
            JobName = jobName;
            Timestamp = DateTime.Now;
            State = Enum.GetName(typeof(BackupState), BackupState.INACTIVE) ?? "UNKNOWN";
            TotalFiles = 0;
            TotalSize = 0;
            RemainingFiles = 0;
            RemainingSize = 0;
            CurrentSourceFile = string.Empty;
            CurrentTargetFile = string.Empty;
        }
    }
}