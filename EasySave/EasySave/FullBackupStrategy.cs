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

        // Les dépendances (comme LoggingService et StateManager) sont passées à Execute
        // Ou elles pourraient être injectées ici si la stratégie a besoin de les utiliser en dehors d'Execute.
        // Pour l'instant, je suppose qu'elles sont fournies à Execute, mais FileSystemService est essentiel.
        public FullBackupStrategy(FileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            _observers = new List<IBackupObserver>();
        }

        // Méthode pour enregistrer des observateurs si la stratégie est le sujet.
        public void RegisterObserver(IBackupObserver observer)
        {
            if (!_observers.Contains(observer)) _observers.Add(observer);
        }

        public void Execute(BackupJob job)
        {
            Console.WriteLine($"FullBackupStrategy: Executing for job '{job.Name}'");
            job.State = BackupState.ACTIVE;

            var filesToBackup = GetFilesToBackup(job);
            long totalSize = filesToBackup.Sum(file => _fileSystemService.GetSize(file));
            int filesProcessed = 0;

            foreach (var sourceFilePath in filesToBackup)
            {
                _observers.ForEach(observer =>
                {
                    observer.Update(
                        job.Name,
                        BackupState.ACTIVE,
                        filesToBackup.Count,
                        totalSize,
                        filesToBackup.Count,
                        totalSize,
                        "Scanning complete",
                        string.Empty
                    );
                });
                string relativePath = sourceFilePath.Substring(job.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFilePath = Path.Combine(job.TargetPath, relativePath);

                try
                {
                    // Assurer que le répertoire cible existe
                    string? targetDir = Path.GetDirectoryName(targetFilePath);
                    if (targetDir != null && !_fileSystemService.DirectoryExists(targetDir))
                    {
                        _fileSystemService.CreateDirectory(targetDir);
                        // TODO: Logger la création du répertoire via le LoggingService passé à Execute
                    }

                    _fileSystemService.CopyFile(sourceFilePath, targetFilePath);
                    filesProcessed++;
                    // TODO: Logger la copie du fichier via le LoggingService passé à Execute
                    // TODO: Mettre à jour le StateManager via le StateManager passé à Execute
                    // (job.Name, job.State, filesToBackup.Count, totalSize, filesToBackup.Count - filesProcessed, totalSize - currentFileSize, sourceFilePath, targetFilePath)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR during full backup of {sourceFilePath}: {ex.Message}");

                    // TODO: Logger l'erreur
                    // TODO: Mettre à jour le StateManager
                }
            }
            job.State = BackupState.COMPLETED; // Ou ERROR si des erreurs se sont produites
            // TODO: Mettre à jour StateManager pour la finalisation
        }

        public List<string> GetFilesToBackup(BackupJob job)
        {
            return _fileSystemService.GetFilesInDirectory(job.SourcePath);
        }
    }
}