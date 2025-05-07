using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LoggingLibrary;

namespace EasySave
{
    public class BackupManager
    {
        public List<BackupJob> backupJobs;
        public StateManager StateManager;
        private FileSystemService _fileSystemService;
        private LogService _logService;

        public BackupManager(FileSystemService fileSystemService, LogService logService)
        {
            backupJobs = new List<BackupJob>();
            StateManager = new StateManager("state.json"); // Assurez-vous d'avoir le bon chemin
            _fileSystemService = fileSystemService;
            _logService = logService;
        }

        public void AddJob(string name, string sourcePath, string targetPath, BackupType type, BackupJob job)
        {
            // Ajoute une nouvelle tâche de sauvegarde
            job.Name = name;
            job.SourcePath = sourcePath;
            job.TargetPath = targetPath;
            job.BackupType = type;
            job.BackupState = BackupState.INACTIVE;
            job.CreationTime = DateTime.Now;
            backupJobs.Add(job);
            StateManager.JobStates.Add(new JobState { JobName = job.Name, State = job.BackupState, LastTimeState = job.LastTimeState }); // Ajouter l'état dans StateManager
            SaveJobs(); // Sauvegarder les jobs après l'ajout
        }

        public bool RemoveJob(int jobIndex)
        {
            // Supprime une tâche de sauvegarde
            if (jobIndex >= 0 && jobIndex < backupJobs.Count)
            {
                string jobName = backupJobs[jobIndex].Name;
                backupJobs.RemoveAt(jobIndex);
                StateManager.JobStates.RemoveAll(js => js.JobName == jobName); // Supprimer l'état dans StateManager
                SaveJobs(); // Sauvegarder les jobs après la suppression
                return true;
            }
            return false;
        }

        public bool UpdateJob(int jobIndex, BackupJob job)
        {
            // Met à jour une tâche de sauvegarde existante
            if (jobIndex >= 0 && jobIndex < backupJobs.Count)
            {
                job.Name = backupJobs[jobIndex].Name; // Garder le nom original
                backupJobs[jobIndex] = job;
                SaveJobs(); // Sauvegarder les jobs après la mise à jour
                return true;
            }
            return false;
        }

        public BackupJob GetJob(int jobIndex)
        {
            // Récupère une tâche de sauvegarde par son index
            if (jobIndex >= 0 && jobIndex < backupJobs.Count)
            {
                return backupJobs[jobIndex];
            }
            return null;
        }

        public void ExecuteJob(int jobIndex)
        {
            // Exécute une tâche de sauvegarde spécifique
            if (jobIndex >= 0 && jobIndex < backupJobs.Count)
            {
                BackupJob job = backupJobs[jobIndex];
                job.BackupState = BackupState.ACTIVE;
                job.LastTimeState = DateTime.Now;
                StateManager.SaveState(); // Sauvegarder l'état avant l'exécution

                try
                {
                    List<string> filesCopied = job.Execute(); // Exécuter la sauvegarde
                    job.BackupState = BackupState.COMPLETED;
                    StateManager.UpdateJobState(job.Name, job.BackupState, job.LastTimeState); // Mettre à jour l'état dans StateManager
                    _logService.Log(new LogEntry
                    {
                        Timestamp = DateTime.Now,
                        EventType = "JobExecuted",
                        Message = $"Tâche '{job.Name}' exécutée avec succès. Fichiers copiés : {filesCopied.Count}"
                    });
                }
                catch (Exception ex)
                {
                    job.BackupState = BackupState.ERROR;
                    StateManager.UpdateJobState(job.Name, job.BackupState, job.LastTimeState); // Mettre à jour l'état dans StateManager
                    _logService.Log(new LogEntry
                    {
                        Timestamp = DateTime.Now,
                        EventType = "JobError",
                        Message = $"Erreur lors de l'exécution de la tâche '{job.Name}': {ex.Message}"
                    });
                    Console.WriteLine($"Erreur lors de l'exécution de la tâche '{job.Name}': {ex.Message}");
                }
                finally
                {
                    StateManager.SaveState(); // Sauvegarder l'état après l'exécution (succès ou échec)
                }
            }
        }

        public void ExecuteAllJobs(int[] jobIndices)
        {
            // Exécute plusieurs tâches de sauvegarde
            foreach (int jobIndex in jobIndices)
            {
                ExecuteJob(jobIndex);
            }
        }

        public void SaveJobs()
        {
            // Sauvegarde la liste des tâches de sauvegarde dans un fichier
            try
            {
                string json = JsonSerializer.Serialize(backupJobs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText("backup_jobs.json", json); // Assurez-vous d'avoir le bon chemin
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde des tâches : {ex.Message}");
            }
        }

        public void LoadJobs()
        {
            // Charge la liste des tâches de sauvegarde depuis un fichier
            try
            {
                if (File.Exists("backup_jobs.json")) // Assurez-vous d'avoir le bon chemin
                {
                    string json = File.ReadAllText("backup_jobs.json");
                    backupJobs = JsonSerializer.Deserialize<List<BackupJob>>(json);

                    // Reconstruire l'état dans StateManager
                    StateManager.JobStates.Clear();
                    foreach (var job in backupJobs)
                    {
                        StateManager.JobStates.Add(new JobState
                        {
                            JobName = job.Name,
                            State = job.BackupState,
                            LastTimeState = job.LastTimeState
                        });
                    }
                }
                else
                {
                    backupJobs = new List<BackupJob>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement des tâches : {ex.Message}");
                backupJobs = new List<BackupJob>(); // Assurer une liste vide en cas d'erreur
            }
        }
    }
}