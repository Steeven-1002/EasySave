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
            var job = new BackupJob(name, sourcePath, targetPath, type, this);

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
        /// Executes the backup jobs with the specified indices using centralized coordination
        /// </summary>
        /// <param name="jobIndexes">List of job indices to execute</param>
        public void ExecuteJobs(ref List<int> jobIndexes)
        {
            var jobsToExecute = new List<BackupJob>();

            // Collect valid jobs
            foreach (var index in jobIndexes)
            {
                if (index >= 0 && index < backupJobs.Count)
                {
                    jobsToExecute.Add(backupJobs[index]);
                }
            }

            if (jobsToExecute.Count == 0) return;

            // Check business software before starting any job
            var businessMonitor = new BusinessApplicationMonitor(AppSettings.Instance.GetSetting("BusinessSoftwareName") as string);
            if (businessMonitor.IsRunning())
            {
                string businessSoftwareName = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
                string message = $"Business software {businessSoftwareName} detected. Cannot start backup jobs.";
                System.Windows.Forms.MessageBox.Show(message, "Business Software Detected",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

                foreach (var job in jobsToExecute)
                {
                    job.Status.SetError(message);
                }
                return;
            }

            // Start all jobs (initialize their status and prepare file lists)
            foreach (var job in jobsToExecute)
            {
                try
                {
                    job.InitializeForExecution();
                }
                catch (Exception ex)
                {
                    job.Status.SetError($"Failed to initialize job: {ex.Message}");
                }
            }

            // Execute centralized processing
            ExecuteJobsCentralized(jobsToExecute);
        }

        /// <summary>
        /// Centralized execution that processes all files from all jobs in a coordinated manner
        /// </summary>
        private void ExecuteJobsCentralized(List<BackupJob> jobs)
        {
            var allFiles = new List<(string filePath, BackupJob job, bool isPriority)>();

            // Collect all files from all jobs
            foreach (var job in jobs)
            {
                if (job.Status.State != BackupState.Running) continue;

                var jobFiles = job.GetPendingFiles().ToList();
                foreach (var file in jobFiles)
                {
                    string ext = Path.GetExtension(file);
                    bool isPriority = PriorityExtensionManager.IsPriorityExtension(ext);
                    allFiles.Add((file, job, isPriority));
                }
            }

            // Sort files: priority files first, then by job order
            var sortedFiles = allFiles
                .OrderByDescending(f => f.isPriority)
                .ThenBy(f => jobs.IndexOf(f.job))
                .ToList();

            // Process all files in the coordinated order
            foreach (var (filePath, job, isPriority) in sortedFiles)
            {
                // Check if any job was stopped or paused
                if (jobs.Any(j => j.IsStopRequested()))
                    break;

                // Check business software before each file
                var businessMonitor = new BusinessApplicationMonitor(AppSettings.Instance.GetSetting("BusinessSoftwareName") as string);
                if (businessMonitor.IsRunning())
                {
                    string businessSoftwareName = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
                    string message = $"Business software {businessSoftwareName} detected during backup.";

                    System.Windows.Forms.MessageBox.Show(
                        $"Business software {businessSoftwareName} detected. Stopping all backup jobs.",
                        "Business Software Detected",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);

                    foreach (var j in jobs)
                    {
                        j.Status.SetError(message);
                    }
                    break;
                }

                // Check if this specific job should skip non-priority files
                if (!isPriority && HasPendingPriorityFilesForJob(job))
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"[IGNORÉ] {filePath} (non prioritaire)\nDes fichiers prioritaires restent à traiter dans le job {job.Name}.",
                        "Ordre de traitement",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                    continue;
                }

                // Display processing message
                if (isPriority)
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"[PRIORITAIRE] {filePath} est traité par le job '{job.Name}'.",
                        "Ordre de traitement",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"[NORMAL] {filePath} est traité par le job '{job.Name}'.",
                        "Ordre de traitement",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                }

                // Process the file
                try
                {
                    job.ProcessSingleFileExternal(filePath);
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"Error processing file {filePath} in job {job.Name}: {ex.Message}",
                        "Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
            }

            // Complete jobs and cleanup
            foreach (var job in jobs)
            {
                try
                {
                    job.FinalizeExecution();
                }
                catch (Exception ex)
                {
                    job.Status.SetError($"Failed to finalize job: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if a job has pending priority files
        /// </summary>
        private bool HasPendingPriorityFilesForJob(BackupJob job)
        {
            return job.GetPendingFiles().Any(file =>
                PriorityExtensionManager.IsPriorityExtension(Path.GetExtension(file))
            );
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
                                var job = new BackupJob(jobData.Name, sourcePath, targetPath, jobData.Type, this);

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

        public bool HasPendingPriorityFiles()
        {
            // Récupère la liste des extensions prioritaires
            var priorityList = new List<string>();
            var setting = AppSettings.Instance.GetSetting("ExtensionFilePriority");
            if (setting is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var ext in jsonElement.EnumerateArray())
                    if (ext.ValueKind == JsonValueKind.String && ext.GetString() is string s)
                        priorityList.Add(s);
            }

            // Vérifie s'il reste des fichiers prioritaires à traiter dans au moins un job en cours
            return backupJobs.Any(job =>
                job.Status.State == BackupState.Running &&
                job.GetPendingFiles().Any(f =>
                    priorityList.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                )
            );
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