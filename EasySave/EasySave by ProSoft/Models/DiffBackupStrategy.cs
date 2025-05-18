using System;
using System.Collections.Generic;
using System.IO;
using EasySave.Services;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Implements differential backup strategy that only copies files that have changed or
    /// do not exist in the target directory
    /// </summary>
    public class DiffBackupStrategy : IBackupFileStrategy
    {
        private readonly FileSystemService _fileSystemService;

        /// <summary>
        /// Initializes a new instance of the DiffBackupStrategy class
        /// </summary>
        public DiffBackupStrategy()
        {
            _fileSystemService = new FileSystemService();
        }

        /// <summary>
        /// Gets files that need to be copied based on differential backup criteria:
        /// - Files that don't exist in target
        /// - Files whose hash has changed since last backup
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

                // Create target directory if it doesn't exist
                if (!Directory.Exists(job.TargetPath))
                {
                    Directory.CreateDirectory(job.TargetPath);
                }

                // Get all files from source
                List<string> sourceFiles = _fileSystemService.GetFilesInDirectory(job.SourcePath);

                // List to store files that need to be copied
                List<string> filesToCopy = new List<string>();
                long totalSize = 0;

                foreach (string sourceFile in sourceFiles)
                {
                    // Get relative path to maintain directory structure
                    string relativePath = sourceFile.Substring(job.SourcePath.Length).TrimStart('\\', '/');
                    string targetFile = Path.Combine(job.TargetPath, relativePath);

                    bool needsCopy = false;

                    // Check if file exists in target
                    if (!File.Exists(targetFile))
                    {
                        // File doesn't exist in target, needs to be copied
                        needsCopy = true;
                    }
                    else
                    {
                        // File exists, compare hashes
                        string sourceHash = _fileSystemService.GetFileHash(sourceFile);
                        string targetHash = _fileSystemService.GetFileHash(targetFile);

                        if (sourceHash != targetHash)
                        {
                            // Hash is different, file has changed
                            needsCopy = true;
                        }
                    }

                    if (needsCopy)
                    {
                        filesToCopy.Add(sourceFile);
                        totalSize += new FileInfo(sourceFile).Length;
                    }
                }

                // Update job status with file count and total size
                job.Status.TotalFiles = filesToCopy.Count;
                job.Status.RemainingFiles = filesToCopy.Count;
                job.Status.TotalSize = totalSize;
                job.Status.RemainingSize = totalSize;

                return filesToCopy;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error getting files for differential backup: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                job.Status.State = BackupState.Error;
                throw;
            }
        }
    }
}
