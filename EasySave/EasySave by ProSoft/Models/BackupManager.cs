using System.Diagnostics;
using System;
using System.IO;
using System.Text.Json;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Manages backup jobs, including creation, execution, and persistence
    /// </summary>
    public class BackupManager
    {
        private List<BackupJob> backupJobs;
        private string jobsConfigFilePath;
        private readonly SemaphoreSlim _largeFileTransferSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the BackupManager class
        /// </summary>
        public BackupManager()
        {
            backupJobs = new List<BackupJob>();
            // Define the jobs configuration file path in ApplicationData/EasySave
            jobsConfigFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                "jobs.json"
            );

            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(jobsConfigFilePath)!);
        }

        /// <summary>
        /// Adds a new backup job with the specified parameters
        /// </summary>
        /// <param name="name">Name of the backup job</param>
        /// <param name="sourcePath">Source directory path</param>
        /// <param name="targetPath">Target directory path</param>
        /// <param name="type">Type of backup (Full or Differential)</param>
        /// <returns>The newly created BackupJob instance</returns>
        public BackupJob AddJob(string name, ref string sourcePath, ref string targetPath, ref BackupType type)
        {
            // Create new backup job
            var job = new BackupJob(name, sourcePath, targetPath, type, this, _largeFileTransferSemaphore);

            // Add to job list
            backupJobs.Add(job);

            // Save jobs to persist the new one
            SaveJobs();

            return job;
        }

        /// <summary>
        /// Removes a backup job at the specified index
        /// </summary>
        /// <param name="jobIndex">Index of the job to remove</param>
        /// <returns>True if job was successfully removed, false otherwise</returns>
        public bool RemoveJob(ref int jobIndex)
        {
            if (jobIndex < 0 || jobIndex >= backupJobs.Count)
            {
                return false;
            }

            backupJobs.RemoveAt(jobIndex);
            SaveJobs();
            return true;
        }

        /// <summary>
        /// Gets a backup job at the specified index
        /// </summary>
        /// <param name="jobIndex">Index of the job to retrieve</param>
        /// <returns>The requested BackupJob instance</returns>
        public BackupJob GetJob(ref int jobIndex)
        {
            if (jobIndex < 0 || jobIndex >= backupJobs.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(jobIndex), "Invalid job index");
            }

            return backupJobs[jobIndex];
        }

        /// <summary>
        /// Gets all backup jobs
        /// </summary>
        /// <returns>List of all backup jobs</returns>
        public List<BackupJob> GetAllJobs()
        {
            return new List<BackupJob>(backupJobs); // Return a copy to prevent direct modification
        }

        /// <summary>
        /// Executes the backup jobs with the specified indices
        /// </summary>
        /// <param name="jobIndexes">List of job indices to execute</param>
        // Dans BackupManager.cs
        public async Task ExecuteJobsAsync(List<int> jobIndexes) // Ou votre mani�re pr�f�r�e d'identifier les travaux
        {
            if (jobIndexes == null || !jobIndexes.Any())
            {
                Debug.WriteLine("BackupManager.ExecuteJobsAsync: Aucun indice de travail fourni.");
                return;
            }
            Debug.WriteLine($"BackupManager.ExecuteJobsAsync: R�ception de {jobIndexes.Count} indice(s) de travail � ex�cuter : [{string.Join(", ", jobIndexes)}]");

            List<Task> runningTasks = new List<Task>();
            List<string> jobNamesToRun = new List<string>(); // Pour la journalisation

            foreach (var index in jobIndexes)
            {
                if (index >= 0 && index < backupJobs.Count)
                {
                    BackupJob jobToRun = backupJobs[index]; // Utilise toujours l'indice ici, voir point suivant
                    jobNamesToRun.Add(jobToRun.Name);

                    Debug.WriteLine($"BackupManager.ExecuteJobsAsync: Cr�ation de la t�che pour le travail '{jobToRun.Name}' (Indice: {index}). Statut actuel: {jobToRun.Status.State}");

                    if (jobToRun.Status.State != BackupState.Initialise) // V�rifie si l'�tat est Initialise
                    {
                        Debug.WriteLine($"AVERTISSEMENT - BackupManager.ExecuteJobsAsync: Le travail '{jobToRun.Name}' n'est pas � l'�tat Initialise (Actuel: {jobToRun.Status.State}). Assurez-vous que ResetForRun() a �t� appel� par le ViewModel.");
                    }

                    runningTasks.Add(Task.Run(() =>
                    {
                        int threadId = Thread.CurrentThread.ManagedThreadId;
                        Debug.WriteLine($"Thread-{threadId}: T�che pour le travail '{jobToRun.Name}' D�MARR�E.");
                        try
                        {
                            jobToRun.Start(); // La m�thode Start de BackupJob
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Thread-{threadId}: EXCEPTION dans la t�che pour le travail '{jobToRun.Name}': {ex.Message}\nStackTrace: {ex.StackTrace}");
                            jobToRun.Status?.SetError($"Erreur lors de l'ex�cution du travail {jobToRun.Name}: {ex.Message}"); // Met � jour le statut en cas d'erreur
                        }
                        Debug.WriteLine($"Thread-{threadId}: T�che pour le travail '{jobToRun.Name}' TERMIN�E. Statut final: {jobToRun.Status.State}");
                    }));
                }
                else
                {
                    Debug.WriteLine($"BackupManager.ExecuteJobsAsync: Indice de travail invalide {index} ignor�. Nombre total de travaux: {backupJobs.Count}");
                }
            }

            if (runningTasks.Any())
            {
                Debug.WriteLine($"BackupManager.ExecuteJobsAsync: En attente de {runningTasks.Count} t�ches pour les travaux : [{string.Join(", ", jobNamesToRun)}]");
                await Task.WhenAll(runningTasks);
                Debug.WriteLine($"BackupManager.ExecuteJobsAsync: Les {runningTasks.Count} t�ches de travail sont toutes termin�es.");
            }
            else
            {
                Debug.WriteLine("BackupManager.ExecuteJobsAsync: Aucune t�che n'a �t� cr��e pour �tre ex�cut�e.");
            }
        }

        public async Task ExecuteJobsByNameAsync(List<string> jobNames)
        {
            if (jobNames == null || !jobNames.Any()) return;

            List<Task> runningTasks = new List<Task>();
            foreach (var name in jobNames)
            {
                BackupJob jobToRun = backupJobs.FirstOrDefault(j => j.Name == name);
                if (jobToRun != null)
                {
                    if (jobToRun.Status.State != BackupState.Initialise) { /* Log warning */ }
                    runningTasks.Add(Task.Run(() => jobToRun.Start())); // Ex�cute la m�thode Start de BackupJob
                }
                else { /* Log error: travail avec ce nom non trouv� */ }
            }
            if (runningTasks.Any()) await Task.WhenAll(runningTasks);
        }

        public bool RemoveJobByName(string jobName) // Au lieu de par indice
        {
            BackupJob jobToRemove = backupJobs.FirstOrDefault(j => j.Name == jobName);
            if (jobToRemove != null)
            {
                backupJobs.Remove(jobToRemove);
                SaveJobs(); // Sauvegarde la configuration des travaux
                return true;
            }
            return false;
        }

        /// <summary>
        /// Loads backup jobs from the configuration file
        /// </summary>
        public void LoadJobs()
        {
            try
            {
                if (File.Exists(jobsConfigFilePath))
                {
                    string jsonContent = File.ReadAllText(jobsConfigFilePath);

                    if (!string.IsNullOrEmpty(jsonContent))
                    {
                        var jobDataList = JsonSerializer.Deserialize<List<JobData>>(jsonContent);

                        if (jobDataList != null)
                        {
                            // Clear existing jobs
                            backupJobs.Clear();

                            // Create jobs from the deserialized data
                            foreach (var jobData in jobDataList)
                            {
                                var type = jobData.Type;
                                var sourcePath = jobData.SourcePath;
                                var targetPath = jobData.TargetPath;

                                // Add the job using existing method
                                var job = new BackupJob(jobData.Name, sourcePath, targetPath, jobData.Type, this, _largeFileTransferSemaphore);


                                backupJobs.Add(job);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Notify user with a popup
                System.Windows.Forms.MessageBox.Show($"Error loading jobs from file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

                // Initialize with empty list if loading fails
                backupJobs = new List<BackupJob>();
            }
        }

        /// <summary>
        /// Saves backup jobs to the configuration file
        /// </summary>
        private void SaveJobs()
        {
            try
            {
                // Convert BackupJob objects to serializable format
                var jobDataList = new List<JobData>();

                foreach (var job in backupJobs)
                {
                    var jobData = new JobData
                    {
                        Name = job.Name,
                        SourcePath = job.SourcePath,
                        TargetPath = job.TargetPath,
                        Type = job.Type
                    };

                    jobDataList.Add(jobData);
                }

                // Serialize and save to file
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonContent = JsonSerializer.Serialize(jobDataList, options);
                File.WriteAllText(jobsConfigFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error saving jobs to file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Private class for serialization of job data
        /// </summary>
        private class JobData
        {
            public string Name { get; set; } = string.Empty;
            public string SourcePath { get; set; } = string.Empty;
            public string TargetPath { get; set; } = string.Empty;
            public BackupType Type { get; set; }
        }
    }
}
