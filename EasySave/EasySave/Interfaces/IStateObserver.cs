using EasySave.Core.Models;

namespace EasySave.Interfaces
{
    /// <summary>
    /// Interface for observing state changes during the backup process.
    /// </summary>
    public interface IStateObserver
    {
        /// <summary>
        /// Notifies observers of a state change in the backup job.
        /// </summary>
        /// <param name="jobName">Backup job name.</param>
        /// <param name="newState">New state of the backup job.</param>
        /// <param name="totalFiles">Number of files to be backed up.</param>
        /// <param name="totalSize">Total size of files to be backed up, in bytes.</param>
        /// <param name="remainingFiles">Number of files remaining to be backed up.</param>
        /// <param name="remainingSize">Size of files remaining to be backed up, in bytes.</param>
        /// <param name="currentSourceFile">Path of the current source file being processed.</param>
        /// <param name="currentTargetFile">Path of the current target file being processed.</param>
        void StateChanged(
            string jobName,
            BackupState newState,
            int totalFiles,
            long totalSize,
            int remainingFiles,
            long remainingSize,
            string currentSourceFile,
            string currentTargetFile);
    }
}