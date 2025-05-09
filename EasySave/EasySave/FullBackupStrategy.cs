using System;
using System.Collections.Generic;
using System.IO;
using EasySave.Models;
using EasySave.Interfaces;
using EasySave.Services;

namespace EasySave.Core
{
    public class FullBackupStrategy : IBackupStrategy
    {
        private readonly FileSystemService _fileSystemService;
        private List<IBackupObserver> _observers;
        private List<IStateObserver> _stateObservers;

        // Les dépendances (comme LoggingService et StateManager) sont passées à Execute
        // Ou elles pourraient être injectées ici si la stratégie a besoin de les utiliser en dehors d'Execute.
        // Pour l'instant, je suppose qu'elles sont fournies à Execute, mais FileSystemService est essentiel.
        public FullBackupStrategy(FileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            _observers = new List<IBackupObserver>();
            _stateObservers = new List<IStateObserver>();
        }

        // Méthode pour enregistrer des observateurs si la stratégie est le sujet.
        public void RegisterObserver(IBackupObserver observer)
        {
            if (!_observers.Contains(observer)) _observers.Add(observer);
        }
        public void UnregisterObserver(IBackupObserver observer)
        {
            if (_observers.Contains(observer)) _observers.Remove(observer);
        }
        public void RegisterStateObserver(IStateObserver stateObserver)
        {
            if (!_stateObservers.Contains(stateObserver)) _stateObservers.Add(stateObserver);
        }
        public void UnregisterStateObserver(IStateObserver stateObserver)
        {
            if (_stateObservers.Contains(stateObserver)) _stateObservers.Remove(stateObserver);
        }

        public void Execute(BackupJob job)
        {
            Console.WriteLine($"FullBackupStrategy: Executing for job '{job.Name}'");
            job.State = BackupState.ACTIVE;

            var filesToBackup = GetFilesToBackup(job);
            long totalSize = filesToBackup.Sum(file => _fileSystemService.GetSize(file));
            int filesProcessed = 0;
            long currentProcessedFileSize = 0;
            bool errorOccurred = false;

            foreach (var sourceFilePath in filesToBackup)
            {
                string relativePath = sourceFilePath.Substring(job.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFilePath = Path.Combine(job.TargetPath, relativePath);
                long currentFileSize = _fileSystemService.GetSize(sourceFilePath);
                currentProcessedFileSize += currentFileSize;

                try
                {
                    // Assurer que le répertoire cible existe
                    string? targetDir = Path.GetDirectoryName(targetFilePath);
                    if (targetDir != null && !_fileSystemService.DirectoryExists(targetDir))
                    {
                        _fileSystemService.CreateDirectory(targetDir);
                        _observers.ForEach(observer =>
                        {
                            observer.Update(
                                job.Name,
                                BackupState.ACTIVE,
                                filesToBackup.Count,
                                totalSize,
                                filesToBackup.Count - filesProcessed,
                                totalSize - currentProcessedFileSize,
                                sourceFilePath,
                                targetFilePath,
                                0 //No transfert duration for directory creation
                                );
                        });
                    }
                    // Start timer for file transfer duration
                    var startTime = DateTime.Now;
                    _fileSystemService.CopyFile(sourceFilePath, targetFilePath);
                    var endTime = DateTime.Now;
                    _observers.ForEach(observer =>
                    {
                        observer.Update(
                            job.Name,
                            BackupState.ACTIVE,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath,
                            targetFilePath,
                            endTime.Subtract(startTime).TotalMilliseconds
                            );
                    });
                    _stateObservers.ForEach(stateObserver =>
                    {
                        stateObserver.StateChanged(
                            job.Name,
                            job.State,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath,
                            targetFilePath
                        );
                    });
                    filesProcessed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR during full backup of {sourceFilePath}: {ex.Message}");
                    errorOccurred = true;
                    _observers.ForEach(observer =>
                    {
                        observer.Update(
                            job.Name,
                            BackupState.ERROR,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath,
                            targetFilePath,
                            -1 // Indicate error in transfer duration
                            );
                    });
                    _stateObservers.ForEach(stateObserver =>
                    {
                        stateObserver.StateChanged(
                            job.Name,
                            job.State,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath, // Pass the current source file
                            targetFilePath  // Pass the current target file
                        );
                    });
                }
            }
            job.State = !errorOccurred ? BackupState.COMPLETED : BackupState.ERROR;
            _stateObservers.ForEach(stateObserver =>
            {
                stateObserver.StateChanged(
                    job.Name,
                    job.State,
                    filesToBackup.Count,
                    totalSize,
                    filesToBackup.Count - filesProcessed,
                    totalSize - currentProcessedFileSize,
                    string.Empty, // No source file for final state
                    string.Empty  // No target file for final state
                );
            });
        }

        public List<string> GetFilesToBackup(BackupJob job)
        {
            return _fileSystemService.GetFilesInDirectory(job.SourcePath);
        }
    }
}