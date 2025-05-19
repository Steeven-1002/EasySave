using EasySave.Services;
using System.IO;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Implements the full backup strategy (all files and folders recursively)
    /// </summary>
    public class FullBackupStrategy : IBackupFileStrategy
    {
        private readonly FileSystemService _fileSystemService;

        /// <summary>
        /// Initializes a new instance of the FullBackupStrategy class
        /// </summary>
        public FullBackupStrategy()
        {
            _fileSystemService = new FileSystemService();
        }

        /// <summary>
        /// Gets all files and folders recursively from the source path of the backup job
        /// </summary>
        /// <param name="job">The backup job to process</param>
        public List<String> GetFiles(ref BackupJob job)
        {
            try
            {
                // Check if source directory exists
                if (!Directory.Exists(job.SourcePath))
                {
                    throw new DirectoryNotFoundException($"Source directory does not exist: {job.SourcePath}");
                }

                // Get all files in the source directory and its subdirectories
                List<string> allFiles = Directory.GetFiles(job.SourcePath, "*.*", SearchOption.AllDirectories).ToList();

                // Update job status with file count and total size
                job.Status.TotalFiles = allFiles.Count;
                job.Status.RemainingFiles = allFiles.Count;

                // Calculate total size of all files
                long totalSize = 0;
                foreach (string file in allFiles)
                {
                    totalSize += new FileInfo(file).Length;
                }

                job.Status.TotalSize = totalSize;
                job.Status.RemainingSize = totalSize;

                return allFiles;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error retrieving files for full backup: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                throw;
            }
        }
    }
}
