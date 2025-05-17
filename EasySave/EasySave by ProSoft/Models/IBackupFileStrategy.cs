using System;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Interface that defines a strategy for retrieving files to be processed in a backup job.
    /// Different implementations handle various backup types (Full, Differential, etc.).
    /// </summary>
    public interface IBackupFileStrategy
    {
        /// <summary>
        /// Retrieves the appropriate files for backup based on the specific strategy.
        /// </summary>
        /// <param name="job">Reference to the backup job containing source and target paths</param>
        /// <remarks>
        /// This method should:
        /// - Analyze source and target directories
        /// - Determine which files need processing according to the strategy
        /// - Update job status with file counts and sizes
        /// - Prepare the job for execution
        /// </remarks>
        void GetFiles(ref BackupJob job);
    }
}