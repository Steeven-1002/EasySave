using System;
using System.Text.Json;

namespace EasySave.Models
{
    /// <summary>
    /// Represents the state of a backup job, including its metadata and progress.
    /// </summary>
    public class JobState
    {
        /// <summary>
        /// Gets or sets the name of the backup job.
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last update to the job state.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the current state of the backup job.
        /// </summary>
        public BackupState State { get; set; }

        /// <summary>
        /// Gets or sets the total number of files to be backed up.
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Gets or sets the total size (in bytes) of all files to be backed up.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Gets or sets the number of files remaining to be backed up.
        /// </summary>
        public int RemainingFiles { get; set; }

        /// <summary>
        /// Gets or sets the total size (in bytes) of the files remaining to be backed up.
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
            State = BackupState.INACTIVE;
            CurrentSourceFile = string.Empty;
            CurrentTargetFile = string.Empty;
        }

        /// <summary>
        /// Serializes the current job state to a JSON string.
        /// </summary>
        /// <returns>A JSON-formatted string representing the job state.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}