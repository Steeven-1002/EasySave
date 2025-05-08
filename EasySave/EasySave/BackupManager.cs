using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EasySave.Models;
using EasySave.Interfaces;
using EasySave.Services;

namespace EasySave.Core
{
    public class BackupManager
    {
        private List<BackupJob> _backupJobs;
        private StateManager _stateManager; // Selon le diagramme
        private string _jobsConfigFilePath = "backup_jobs_config.json"; // Fichier de config pour les jobs

        // IBackupJobFactory n'est pas dans le diagramme, donc création directe.
        // Dépendances comme ILogger, FileSystemService seront passées aux stratégies par BackupJob.Execute
        // ou si BackupManager doit les utiliser directement.

        public BackupManager(StateManager stateManager, ConfigManager configManager) // Ajout de ConfigManager pour le chemin
        {
            _backupJobs = new List<BackupJob>();
            _stateManager = stateManager;
            _jobsConfigFilePath = configManager.GetSetting("BackupJobsFilePath") as string ?? "backup_jobs_config.json";
            LoadJobs();
        }

        public BackupJob? AddJob(string name, string sourcePath, string targetPath, EasySave.Models.BackupType type)
        {
            if (_backupJobs.Count >= 5)
            {
                Console.WriteLine("BackupManager ERROR: Maximum number of backup jobs (5) reached.");
                return null;
            }
            if (_backupJobs.Any(j => j.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"BackupManager ERROR: Job with name '{name}' already exists.");
                return null;
            }

            IBackupStrategy strategy;
            FileSystemService fileSystemService = new FileSystemService(); // Instanciation directe ou via injection

            switch (type)
            {
                case EasySave.Models.BackupType.FULL:
                    strategy = new FullBackupStrategy(fileSystemService);
                    break;
                case EasySave.Models.BackupType.DIFFERENTIAL:
                    strategy = new DifferentialBackupStrategy(fileSystemService);
                    break;
                default:
                    Console.WriteLine($"BackupManager ERROR: Unsupported backup type '{type}'.");
                    return null;
            }

            var newJob = new BackupJob(name, sourcePath, targetPath, type, strategy);
            _backupJobs.Add(newJob);
            SaveJobs();
            Console.WriteLine($"BackupManager: Job '{name}' added.");
            return newJob;
        }

        public bool RemoveJob(int jobIndex)
        {
            if (jobIndex >= 0 && jobIndex < _backupJobs.Count)
            {
                Console.WriteLine($"BackupManager: Job '{_backupJobs[jobIndex].Name}' removed.");
                _backupJobs.RemoveAt(jobIndex);
                SaveJobs();
                return true;
            }
            Console.WriteLine($"BackupManager ERROR: Invalid job index '{jobIndex}' for removal.");
            return false;
        }

        public bool UpdateJob(int jobIndex, BackupJob updatedJobData)
        {
            if (jobIndex >= 0 && jobIndex < _backupJobs.Count)
            {
                // Vérifier si le nouveau nom existe déjà (sauf si c'est le même job)
                if (_backupJobs.Any(j => j.Name.Equals(updatedJobData.Name, StringComparison.OrdinalIgnoreCase) && _backupJobs.IndexOf(j) != jobIndex))
                {
                    Console.WriteLine($"BackupManager ERROR: Another job with name '{updatedJobData.Name}' already exists.");
                    return false;
                }
                // Il faut aussi potentiellement recréer/mettre à jour la stratégie si le type a changé
                if (_backupJobs[jobIndex].Type != updatedJobData.Type)
                {
                    IBackupStrategy newStrategy;
                    FileSystemService fsService = new FileSystemService();

                    if (!updatedJobData.Type.Equals(BackupType.FULL))
                    {
                        newStrategy = new DifferentialBackupStrategy(fsService);
                    }
                    else
                    {
                        newStrategy = new FullBackupStrategy(fsService);
                    }
                }
                else
                {
                    updatedJobData.Strategy = _backupJobs[jobIndex].Strategy; // Conserver l'ancienne stratégie si type inchangé
                }

                _backupJobs[jobIndex] = updatedJobData;
                SaveJobs();
                Console.WriteLine($"BackupManager: Job '{updatedJobData.Name}' updated.");
                return true;
            }
            Console.WriteLine($"BackupManager ERROR: Invalid job index '{jobIndex}' for update.");
            return false;
        }

        public BackupJob? GetJob(int jobIndex)
        {
            if (jobIndex >= 0 && jobIndex < _backupJobs.Count)
            {
                return _backupJobs[jobIndex];
            }
            return null;
        }

        public List<BackupJob> GetAllJobs() => new List<BackupJob>(_backupJobs);


        public void ExecuteJob(int jobIndex)
        {
            BackupJob? job = GetJob(jobIndex);
            if (job != null)
            {
                Console.WriteLine($"BackupManager: Executing job '{job.Name}'...");
                // L'état et la journalisation sont gérés dans job.Execute() via sa stratégie
                // et les observateurs enregistrés.
                // Le StateManager et le LoggingService doivent être passés ou accessibles.
                // Pour ce faire, Program va devoir injecter ces dépendances lors de l'appel.
                // Cette méthode sera appelée par Program.cs qui aura accès à ces services.
                // Pour l'instant, on appelle job.Execute() et on suppose que les dépendances
                // sont gérées plus haut (par ex. Program les passe à job.Execute).
                // Si BackupManager doit passer des dépendances aux stratégies,
                // il aurait besoin de les avoir (ex: ILogger, FileSystemService)
                job.Execute();
            }
            else
            {
                Console.WriteLine($"BackupManager ERROR: Job at index {jobIndex} not found.");
            }
        }

        public void ExecuteJobs(int[] jobIndexes)
        {
            foreach (int index in jobIndexes)
            {
                ExecuteJob(index); // Exécution séquentielle
            }
        }

        // DTO pour la sérialisation afin d'éviter les problèmes avec IBackupStrategy
        private class BackupJobDtoForSerialization
        {
            public string Name { get; set; }
            public string SourcePath { get; set; }
            public string TargetPath { get; set; }
            public BackupType Type { get; set; }
            public DateTime LastRunTime { get; set; }
            public DateTime CreationTime { get; set; }
            // State n'est pas persisté ici, il l'est par StateManager
        }

        public void SaveJobs()
        {
            try
            {
                var dtos = _backupJobs.Select(j => new BackupJobDtoForSerialization
                {
                    Name = j.Name,
                    SourcePath = j.SourcePath,
                    TargetPath = j.TargetPath,
                    Type = (BackupType)j.Type,
                    LastRunTime = j.LastRunTime,
                    CreationTime = j.CreationTime
                }).ToList();
                string json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_jobsConfigFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BackupManager ERROR saving jobs to '{_jobsConfigFilePath}': {ex.Message}");
            }
        }

        public void LoadJobs()
        {
            try
            {
                if (File.Exists(_jobsConfigFilePath))
                {
                    string json = File.ReadAllText(_jobsConfigFilePath);
                    var dtos = JsonSerializer.Deserialize<List<BackupJobDtoForSerialization>>(json);
                    if (dtos != null)
                    {
                        _backupJobs = dtos.Select(dto => {
                            FileSystemService fsService = new FileSystemService(); // Ou injecter
                            IBackupStrategy strategy = dto.Type == BackupType.FULL ?
                                (IBackupStrategy)new FullBackupStrategy(fsService) :
                                new DifferentialBackupStrategy(fsService);
                            return new BackupJob(dto.Name, dto.SourcePath, dto.TargetPath, (Models.BackupType)dto.Type, strategy)
                            {
                                LastRunTime = dto.LastRunTime,
                                CreationTime = dto.CreationTime,
                                State = BackupState.INACTIVE // État initial au chargement
                            };
                        }).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BackupManager ERROR loading jobs from '{_jobsConfigFilePath}': {ex.Message}");
                _backupJobs = new List<BackupJob>(); // Assurer une liste vide en cas d'erreur
            }
        }
    }
}