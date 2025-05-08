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
            NotifyObservers(job, BackupStatus.STARTED);
            job.State = BackupState.ACTIVE;

            var filesToBackup = GetFilesToBackup(job);
            // ... Logique similaire à FullBackupStrategy pour la copie et la notification ...
            // La principale différence est dans GetFilesToBackup

            long totalSize = 0; // TODO: Calculer la taille totale
            int filesProcessed = 0;
            // TODO: Informer StateManager: job.Name, BackupState.ACTIVE, filesToBackup.Count, totalSize, filesToBackup.Count, totalSize, "Scanning complete", ""


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
                        // TODO: Logger création dir
                    }
                    _fileSystemService.CopyFile(sourceFilePath, targetFilePath);
                    filesProcessed++;
                    NotifyObservers(job, BackupStatus.FILE_COPIED);
                    // TODO: Logger la copie du fichier
                    // TODO: Mettre à jour le StateManager
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR during differential backup of {sourceFilePath}: {ex.Message}");
                    NotifyObservers(job, BackupStatus.ERROR);
                    // TODO: Logger l'erreur
                    // TODO: Mettre à jour le StateManager
                }
            }
            job.State = BackupState.COMPLETED; // Ou ERROR
            NotifyObservers(job, job.State == BackupState.COMPLETED ? BackupStatus.COMPLETED_SUCCESS : BackupStatus.COMPLETED_WITH_ERRORS);
            // TODO: Mettre à jour StateManager pour la finalisation
        }

        public List<string> GetFilesToBackup(BackupJob job)
        {
            var filesToBackup = new List<string>();
            var sourceFiles = _fileSystemService.GetFilesInDirectory(job.SourcePath);

            foreach (var sourceFilePath in sourceFiles)
            {
                string relativePath = sourceFilePath.Substring(job.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFilePath = Path.Combine(job.TargetPath, relativePath);

                // Vérifier si le fichier existe dans le répertoire cible
                if (!_fileSystemService.FileExists(targetFilePath))
                {
                    filesToBackup.Add(sourceFilePath);
                    continue;
                }

                // Comparer le hash du fichier source et du fichier cible
                string sourceHash = _fileSystemService.GetFileHash(sourceFilePath);
                string targetHash = _fileSystemService.GetFileHash(targetFilePath);

                if (sourceHash != targetHash)
                {
                    filesToBackup.Add(sourceFilePath);
                }
            }

            return filesToBackup;
        }

        private void NotifyObservers(BackupJob job, BackupStatus status)
        {
            foreach (var observer in _observers)
            {
                observer.Update(job, status);
            }
        }
    }
}