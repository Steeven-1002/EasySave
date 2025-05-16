using System.Text.Json;
using EasySave.Core.Models;
using EasySave.Interfaces;
using EasySave.Services;

namespace EasySave.Core
{
    /// <summary>
    /// Manages backup jobs, including adding, removing, updating, executing, and persisting them.
    /// </summary>
    public class BackupManager
    {
        private List<BackupJob> _backupJobs;
        private readonly string _jobsConfigFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackupManager"/> class.
        /// </summary>
        /// <param name="configManager">The configuration manager to retrieve settings.</param>
        public BackupManager(ConfigManager configManager)
        {
            _backupJobs = new List<BackupJob>();
            _jobsConfigFilePath = configManager.GetSetting("BackupJobsFilePath") as string ?? "backup_jobs_config.json";
            LoadJobs();
        }

        /// <summary>
        /// Adds a new backup job.
        /// </summary>
        /// <param name="name">The name of the backup job.</param>
        /// <param name="sourcePath">The source directory path.</param>
        /// <param name="targetPath">The target directory path.</param>
        /// <param name="type">The type of backup (Full or Differential).</param>
        /// <returns>The created <see cref="BackupJob"/> if successful, otherwise null.</returns>
        public BackupJob? AddJob(string name, string sourcePath, string targetPath, BackupType type)
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
            FileSystemService fileSystemService = new FileSystemService();

            switch (type)
            {
                case BackupType.FULL:
                    strategy = new FullBackupStrategy(fileSystemService);
                    break;
                case BackupType.DIFFERENTIAL:
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

        /// <summary>
        /// Removes a backup job by its index.
        /// </summary>
        /// <param name="jobIndex">The index of the job to remove.</param>
        /// <returns>True if the job was removed successfully, otherwise false.</returns>
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

        /// <summary>
        /// Updates an existing backup job.
        /// </summary>
        /// <param name="jobIndex">The index of the job to update.</param>
        /// <param name="updatedJobData">The updated job data.</param>
        /// <returns>True if the job was updated successfully, otherwise false.</returns>
        public bool UpdateJob(int jobIndex, BackupJob updatedJobData)
        {
            if (jobIndex >= 0 && jobIndex < _backupJobs.Count)
            {
                if (_backupJobs.Any(j => j.Name.Equals(updatedJobData.Name, StringComparison.OrdinalIgnoreCase) && _backupJobs.IndexOf(j) != jobIndex))
                {
                    Console.WriteLine($"BackupManager ERROR: Another job with name '{updatedJobData.Name}' already exists.");
                    return false;
                }

                if (_backupJobs[jobIndex].Type != updatedJobData.Type)
                {
                    FileSystemService fsService = new FileSystemService();
                    if (updatedJobData.Type == BackupType.FULL)
                        updatedJobData.Strategy = new FullBackupStrategy(fsService);
                    else
                        updatedJobData.Strategy = new DifferentialBackupStrategy(fsService);

                    // Réenregistrement des observateurs
                    updatedJobData.Strategy.RegisterObserver(LoggingBackup.Instance);
                    updatedJobData.Strategy.RegisterStateObserver(StateManager.Instance);
                }
                else
                {
                    updatedJobData.Strategy = _backupJobs[jobIndex].Strategy;
                }

                _backupJobs[jobIndex] = updatedJobData;
                SaveJobs();
                Console.WriteLine($"BackupManager: Job '{updatedJobData.Name}' updated.");
                return true;
            }
            Console.WriteLine($"BackupManager ERROR: Invalid job index '{jobIndex}' for update.");
            return false;
        }

        /// <summary>
        /// Retrieves a backup job by its index.
        /// </summary>
        /// <param name="jobIndex">The index of the job to retrieve.</param>
        /// <returns>The <see cref="BackupJob"/> if found, otherwise null.</returns>
        public BackupJob? GetJob(int jobIndex)
        {
            if (jobIndex >= 0 && jobIndex < _backupJobs.Count)
            {
                return _backupJobs[jobIndex];
            }
            return null;
        }

        /// <summary>
        /// Retrieves all backup jobs.
        /// </summary>
        /// <returns>A list of all <see cref="BackupJob"/> instances.</returns>
        public List<BackupJob> GetAllJobs() => [.._backupJobs];

        /// <summary>
        /// Executes a specific backup job by its index.
        /// </summary>
        /// <param name="jobIndex">The index of the job to execute.</param>
        public void ExecuteJob(int jobIndex)
        {
            BackupJob? job = GetJob(jobIndex);
            if (job != null)
            {
                Console.WriteLine($"BackupManager: Executing job '{job.Name}'...");
                job.Execute();
            }
            else
            {
                Console.WriteLine($"BackupManager ERROR: Job at index {jobIndex} not found.");
            }
        }

        /// <summary>
        /// Executes multiple backup jobs by their indexes.
        /// </summary>
        /// <param name="jobIndexes">An array of job indexes to execute.</param>
        public void ExecuteJobs(int[] jobIndexes)
        {
            foreach (int index in jobIndexes)
            {
                ExecuteJob(index);
            }
        }

        /// <summary>
        /// Saves all backup jobs to a configuration file.
        /// </summary>
        private void SaveJobs()
        {
            try
            {
                var dtos = _backupJobs.Select(j => new BackupJobDtoForSerialization
                {
                    Name = j.Name,
                    SourcePath = j.SourcePath,
                    TargetPath = j.TargetPath,
                    Type = j.Type,
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

        /// <summary>
        /// Loads all backup jobs from a configuration file.
        /// </summary>
        private void LoadJobs()
        {
            try
            {
                if (File.Exists(_jobsConfigFilePath))
                {
                    string json = File.ReadAllText(_jobsConfigFilePath);
                    var dtos = JsonSerializer.Deserialize<List<BackupJobDtoForSerialization>>(json);
                    if (dtos != null)
                    {
                        _backupJobs = dtos.Select(dto =>
                        {
                            FileSystemService fsService = new FileSystemService();
                            IBackupStrategy strategy = dto.Type == BackupType.FULL ?
                                new FullBackupStrategy(fsService) :
                                new DifferentialBackupStrategy(fsService);
                            return new BackupJob(dto.Name, dto.SourcePath, dto.TargetPath, dto.Type, strategy)
                            {
                                LastRunTime = dto.LastRunTime,
                                CreationTime = dto.CreationTime,
                                State = BackupState.INACTIVE
                            };
                        }).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BackupManager ERROR loading jobs from '{_jobsConfigFilePath}': {ex.Message}");
                _backupJobs = new List<BackupJob>();
            }
        }

        /// <summary>
        /// DTO class for serializing and deserializing backup jobs.
        /// </summary>
        private class BackupJobDtoForSerialization
        {
            public required string Name { get; init; }
            public required string SourcePath { get; init; }
            public required string TargetPath { get; init; }
            public BackupType Type { get; init; }
            public DateTime LastRunTime { get; init; }
            public DateTime CreationTime { get; init; }
        }
    }
}