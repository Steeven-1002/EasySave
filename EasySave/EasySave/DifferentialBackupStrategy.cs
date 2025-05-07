using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LoggingLibrary;

namespace EasySave
{
    public class DifferentialBackupStrategy : IBackupStrategy
    {
        private FileSystemService _fileSystemService;
        private LogService _logService;

        public DifferentialBackupStrategy(FileSystemService fileSystemService, LogService logService)
        {
            _fileSystemService = fileSystemService;
            _logService = logService;
        }

        public List<string> ExecuteBackup(BackupJob job)
        {
            List<string> filesCopied = new List<string>();
            try
            {
                // 1. Get files to backup
                List<string> filesToBackup = GetFilesToBackup(job);

                // 2. Perform the differential backup
                foreach (string sourceFile in filesToBackup)
                {
                    string relativePath = _fileSystemService.GetRelativePath(job.SourcePath, sourceFile);
                    string targetFile = Path.Combine(job.TargetPath, relativePath);

                    _fileSystemService.CopyFile(sourceFile, targetFile);
                    filesCopied.Add(targetFile);

                    _logService.Log(new LogEntry
                    {
                        Timestamp = DateTime.Now,
                        EventType = "FileCopied",
                        Message = $"Fichier copié : {sourceFile} vers {targetFile}"
                    });
                }

                return filesCopied;
            }
            catch (Exception ex)
            {
                _logService.Log(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    EventType = "BackupError",
                    Message = $"Erreur lors de la sauvegarde différentielle : {ex.Message}"
                });
                throw; // Rethrow the exception to be handled by the BackupManager
            }
        }

        public List<string> GetFilesToBackup(BackupJob job)
        {
            List<string> filesToBackup = new List<string>();
            try
            {
                // Get all files in the source directory
                List<string> allSourceFiles = _fileSystemService.GetAllFiles(job.SourcePath);

                // Determine the last full backup time for this job
                DateTime lastFullBackupTime = GetLastFullBackupTime(job);

                // Filter files based on modification time
                foreach (string sourceFile in allSourceFiles)
                {
                    DateTime lastWriteTime = File.GetLastWriteTime(sourceFile);
                    if (lastWriteTime > lastFullBackupTime)
                    {
                        filesToBackup.Add(sourceFile);
                    }
                }

                return filesToBackup;
            }
            catch (Exception ex)
            {
                _logService.Log(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    EventType = "BackupError",
                    Message = $"Erreur lors de la récupération des fichiers à sauvegarder : {ex.Message}"
                });
                throw;
            }
        }

        private DateTime GetLastFullBackupTime(BackupJob job)
        {
            // Logic to determine the last full backup time for this job.
            // This could involve reading metadata, checking log files,
            // or using any other relevant information.

            // For simplicity, let's assume it's the job's creation time if it's the first differential backup.
            // Otherwise, you'll need to implement the logic to retrieve the correct time.

            // !! IMPORTANT !!: This is a placeholder. You MUST implement the correct logic.
            return job.CreationTime;
        }

        public void CopyFileWithResourceControl(string sourceFilePath, string targetFilePath, DateTime lastBackupTime)
        {
            try
            {
                // Implement logic to copy the file while considering resource control
                // (e.g., checking for file locks, retrying, etc.)

                // !! IMPORTANT !!: This is a placeholder. You MUST implement the correct logic.
                _fileSystemService.CopyFile(sourceFilePath, targetFilePath);
            }
            catch (Exception ex)
            {
                _logService.Log(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    EventType = "FileCopyError",
                    Message = $"Erreur lors de la copie du fichier {sourceFilePath} vers {targetFilePath}: {ex.Message}"
                });
                throw;
            }
        }
    }
}