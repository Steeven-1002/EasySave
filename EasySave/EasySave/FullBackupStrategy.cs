using System;
using System.Collections.Generic;
using System.IO;
using LoggingLibrary;

namespace EasySave
{
    public class FullBackupStrategy : IBackupStrategy
    {
        private FileSystemService _fileSystemService;
        private LogService _logService;

        public FullBackupStrategy(FileSystemService fileSystemService, LogService logService)
        {
            _fileSystemService = fileSystemService;
            _logService = logService;
        }

        public List<string> ExecuteBackup(BackupJob job)
        {
            List<string> filesCopied = new List<string>();
            try
            {
                // 1. Get all files to backup
                List<string> filesToBackup = GetFilesToBackup(job);

                // 2. Perform the full backup
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
                    Message = $"Erreur lors de la sauvegarde complète : {ex.Message}"
                });
                throw; // Rethrow the exception to be handled by the BackupManager
            }
        }

        public List<string> GetFilesToBackup(BackupJob job)
        {
            // For a full backup, we simply get all files from the source
            return _fileSystemService.GetAllFiles(job.SourcePath);
        }

        public void CopyFilesWithResourceControl(string sourceDirectory, string targetDirectory)
        {
            try
            {
                // Implement logic to copy all files from source to target while handling
                // potential resource conflicts (e.g., file locks).

                // !! IMPORTANT !!: This is a placeholder. You MUST implement the correct logic.
                _fileSystemService.CopyDirectory(sourceDirectory, targetDirectory);
            }
            catch (Exception ex)
            {
                _logService.Log(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    EventType = "FileCopyError",
                    Message = $"Erreur lors de la copie des fichiers de {sourceDirectory} vers {targetDirectory}: {ex.Message}"
                });
                throw;
            }
        }
    }
}