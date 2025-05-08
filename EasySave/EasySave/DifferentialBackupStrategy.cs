using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EasySave.Models;
using EasySave.Interfaces;
using EasySave.Services;

namespace EasySave.Core
{
    public class DifferentialBackupStrategy : IBackupStrategy
    {
        private readonly FileSystemService _fileSystemService;
        private List<IBackupObserver> _observers;

        public DifferentialBackupStrategy(FileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            _observers = new List<IBackupObserver>();
        }

        public void RegisterObserver(IBackupObserver observer)
        {
            if (!_observers.Contains(observer)) _observers.Add(observer);
        }

        public void Execute(BackupJob job)
        {
            Console.WriteLine($"DifferentialBackupStrategy: Executing for job '{job.Name}'");
            job.State = BackupState.ACTIVE;

            var filesToBackup = GetFilesToBackup(job);

            long totalSize = filesToBackup.Sum(file => _fileSystemService.GetSize(file));
            int totalFiles = filesToBackup.Count;
            int filesProcessed = 0;

            NotifyObservers(job.Name, BackupState.ACTIVE, totalFiles, totalSize, totalFiles, totalSize, "Scanning complete", "");

            foreach (var sourceFilePath in filesToBackup)
            {
                string relativePath = sourceFilePath.Substring(job.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFilePath = Path.Combine(job.TargetPath, relativePath);

                try
                {
                    string? targetDir = Path.GetDirectoryName(targetFilePath);
                    if (targetDir != null && !_fileSystemService.DirectoryExists(targetDir))
                    {
                        _fileSystemService.CreateDirectory(targetDir);
                    }
                    _fileSystemService.CopyFile(sourceFilePath, targetFilePath);
                    filesProcessed++;
                    NotifyObservers(job.Name, BackupState.ACTIVE, totalFiles, totalSize, totalFiles - filesProcessed, totalSize, sourceFilePath, targetFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR during differential backup of {sourceFilePath}: {ex.Message}");
                    NotifyObservers(job.Name, BackupState.ERROR, totalFiles, totalSize, totalFiles - filesProcessed, totalSize, sourceFilePath, targetFilePath);
                }
            }

            job.State = filesProcessed == totalFiles ? BackupState.COMPLETED : BackupState.ERROR;
            NotifyObservers(job.Name, job.State, totalFiles, totalSize, 0, 0, "", "");
        }

        public List<string> GetFilesToBackup(BackupJob job)
        {
            var filesToBackup = new List<string>();
            var sourceFiles = _fileSystemService.GetFilesInDirectory(job.SourcePath);

            foreach (var sourceFilePath in sourceFiles)
            {
                string relativePath = sourceFilePath.Substring(job.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFilePath = Path.Combine(job.TargetPath, relativePath);

                if (!_fileSystemService.FileExists(targetFilePath))
                {
                    filesToBackup.Add(sourceFilePath);
                    continue;
                }

                string sourceHash = _fileSystemService.GetFileHash(sourceFilePath);
                string targetHash = _fileSystemService.GetFileHash(targetFilePath);

                if (sourceHash != targetHash)
                {
                    filesToBackup.Add(sourceFilePath);
                }
            }

            return filesToBackup;
        }

        private void NotifyObservers(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile)
        {
            foreach (var observer in _observers)
            {
                observer.Update(jobName, newState, totalFiles, totalSize, remainingFiles, remainingSize, currentSourceFile, currentTargetFile);
            }
        }
    }
}