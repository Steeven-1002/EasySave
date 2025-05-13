using EasySave.Core.Models;

namespace EasySave.Interfaces;

/// <summary>
/// Interface for observing backup job updates.
/// </summary>
public interface IBackupObserver
{
    /// <summary>
    /// Updates the observer with the current state of a backup job.
    /// </summary>
    /// <param name="jobName">The name of the backup job.</param>
    /// <param name="newState">The new state of the backup job.</param>
    /// <param name="totalFiles">The total number of files in the backup job.</param>
    /// <param name="totalSize">The total size of all files in the backup job, in bytes.</param>
    /// <param name="remainingFiles">The number of files remaining to be processed.</param>
    /// <param name="remainingSize">The size of the remaining files to be processed, in bytes.</param>
    /// <param name="currentSourceFile">The path of the current source file being processed.</param>
    /// <param name="currentTargetFile">The path of the current target file being processed.</param>
    /// <param name="transfertDuration">The duration of the file transfer, in seconds.</param>
    void Update(
        string jobName,
        BackupState newState,
        int totalFiles,
        long totalSize,
        int remainingFiles,
        long remainingSize,
        string currentSourceFile,
        string currentTargetFile,
        double transfertDuration);
}