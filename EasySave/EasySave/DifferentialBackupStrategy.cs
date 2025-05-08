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
            // La logique ici est cruciale: comparer les fichiers source avec ceux de la cible
            // ou se baser sur job.LastRunTime (qui devrait être la date de la dernière sauvegarde complète).
            // Une implémentation simple basée sur la date de modification depuis la dernière sauvegarde :
            DateTime since = job.LastRunTime; // Idéalement, ce serait la date de la dernière *Full* Backup réussie.
            if (since == DateTime.MinValue)
            {
                // Pas de sauvegarde précédente, donc c'est comme une sauvegarde complète.
                Console.WriteLine($"Differential for '{job.Name}': No previous backup, acting as FULL.");
                return _fileSystemService.GetFilesInDirectory(job.SourcePath);
            }
            return _fileSystemService.GetModifiedFilesSince(job.SourcePath, since);
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