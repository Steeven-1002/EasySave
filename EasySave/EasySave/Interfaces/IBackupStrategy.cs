using EasySave.Core.Models;
using EasySave.Services;

namespace EasySave.Interfaces
{
    /// <summary>
    /// Defines the strategy for executing backup jobs and managing observers.
    /// </summary>
    public interface IBackupStrategy
    {
        /// <summary>
        /// Executes the specified backup job.
        /// </summary>
        /// <param name="job">The backup job to execute.</param>
        void Execute(BackupJob job);

        /// <summary>
        /// Registers an observer to receive updates about the backup process.
        /// </summary>
        /// <param name="observer">The observer to register.</param>
        void RegisterObserver(IBackupObserver observer);

        /// <summary>
        /// Registers a state observer to monitor changes in the backup job's state.
        /// </summary>
        /// <param name="observer">The state observer to register.</param>
        void RegisterStateObserver(IStateObserver observer);

        /// <summary>
        /// Retrieves the list of files to be backed up for the specified backup job.
        /// </summary>
        /// <param name="job">The backup job for which to retrieve the files.</param>
        /// <returns>A list of file paths to be backed up.</returns>
        List<string> GetFilesToBackup(BackupJob job);
        void RegisterObserver(Func<string, LoggingBackup> instance);
    }
}