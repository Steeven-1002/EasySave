using EasySave_by_ProSoft.Core;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Manages backup jobs, including creation, execution, and persistence
    /// </summary>
    public class BackupManager : IEventListener
    {
        private List<BackupJob> backupJobs;
        private string jobsConfigFilePath;
        private readonly ParallelExecutionManager _parallelManager;
        private readonly BusinessApplicationMonitor _businessMonitor;
        private readonly object _priorityLock = new();
        private readonly EventManager _eventManager = EventManager.Instance;
        private Services.RemoteControlService _remoteControlService;

        // Singleton instance
        private static BackupManager? _instance;

        // Lock for thread safety
        private static readonly object _lock = new();

        /// <summary>
        /// Singleton instance property
        /// </summary>
        public static BackupManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new BackupManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Private constructor to prevent direct instantiation
        /// </summary>
        private BackupManager()
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

            // Initialize the parallel execution manager
            _parallelManager = new ParallelExecutionManager();

            // Initialize the business application monitor
            string appNameToMonitor = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
            _businessMonitor = new BusinessApplicationMonitor(appNameToMonitor);
            _businessMonitor.BusinessAppStarted += BusinessMonitor_BusinessAppStarted;
            _businessMonitor.BusinessAppStopped += BusinessMonitor_BusinessAppStopped;
            _businessMonitor.StartMonitoring();

            // Register as an event listener
            _eventManager.AddListener(this);
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
            var job = new BackupJob(name, sourcePath, targetPath, type, this, _parallelManager.LargeFileTransferSemaphore);

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
        /// Executes the backup jobs with the specified indices using the ParallelExecutionManager
        /// </summary>
        /// <param name="jobIndexes">List of job indices to execute</param>
        public async Task ExecuteJobsAsync(List<int> jobIndexes)
        {
            if (jobIndexes == null || !jobIndexes.Any())
            {
                Debug.WriteLine("BackupManager.ExecuteJobsAsync: No job indices provided.");
                return;
            }
            Debug.WriteLine($"BackupManager.ExecuteJobsAsync: Received {jobIndexes.Count} job index(es) to execute: [{string.Join(", ", jobIndexes)}]");

            List<BackupJob> jobsToRun = new List<BackupJob>();

            foreach (var index in jobIndexes)
            {
                if (index >= 0 && index < backupJobs.Count)
                {
                    BackupJob jobToRun = backupJobs[index];
                    jobsToRun.Add(jobToRun);

                    Debug.WriteLine($"BackupManager.ExecuteJobsAsync: Adding job '{jobToRun.Name}' (Index: {index}) to execution list. Current state: {jobToRun.Status.State}");

                    // Register the job with business application monitor
                    _businessMonitor.RegisterJob(jobToRun);

                    if (jobToRun.Status.State != BackupState.Initialise)
                    {
                        Debug.WriteLine($"WARNING - BackupManager.ExecuteJobsAsync: Job '{jobToRun.Name}' is not in Initialize state (Current: {jobToRun.Status.State}). Make sure ResetForRun() was called by the ViewModel.");
                    }
                }
                else
                {
                    Debug.WriteLine($"BackupManager.ExecuteJobsAsync: Invalid job index {index} ignored. Total jobs count: {backupJobs.Count}");
                }
            }

            // Run jobs without checking business software state (monitor will handle interruption)
            await _parallelManager.ExecuteJobsInParallelAsync(jobsToRun);
        }

        public async Task ExecuteJobsByNameAsync(List<string> jobNames)
        {
            if (jobNames == null || !jobNames.Any())
                return;

            Debug.WriteLine($"BackupManager.ExecuteJobsByNameAsync: Preparing to execute jobs: {string.Join(", ", jobNames)}");
            
            // Check if business software is running
            if (_businessMonitor != null && _businessMonitor.IsRunning())
            {
                Debug.WriteLine("BackupManager.ExecuteJobsByNameAsync: Business software is running, cannot launch jobs");
                return;
            }

            List<BackupJob> jobsToRun = new List<BackupJob>();

            foreach (var name in jobNames)
            {
                BackupJob jobToRun = backupJobs.FirstOrDefault(j => j.Name == name);
                if (jobToRun != null)
                {
                    if (jobToRun.Status.State == BackupState.Initialise ||
                        jobToRun.Status.State == BackupState.Error ||
                        jobToRun.Status.State == BackupState.Completed)
                    {
                        jobToRun.Status.ResetForRun();
                        Debug.WriteLine($"BackupManager.ExecuteJobsByNameAsync: Reset job '{jobToRun.Name}' for execution");
                    }

                    if (jobToRun.Status.State == BackupState.Initialise)
                    {
                        jobsToRun.Add(jobToRun);
                        _businessMonitor.RegisterJob(jobToRun);
                        Debug.WriteLine($"BackupManager.ExecuteJobsByNameAsync: Added job '{jobToRun.Name}' to execution list");
                    }
                    else
                    {
                        Debug.WriteLine($"BackupManager.ExecuteJobsByNameAsync: Job '{jobToRun.Name}' is not launchable (Actual State: {jobToRun.Status.State})");
                    }
                }
                else
                {
                    Debug.WriteLine($"BackupManager.ExecuteJobsByNameAsync: Job with name '{name}' not found");
                }
            }

            if (jobsToRun.Any())
            {
                Debug.WriteLine($"BackupManager.ExecuteJobsByNameAsync: Executing {jobsToRun.Count} jobs");
                await _parallelManager.ExecuteJobsInParallelAsync(jobsToRun);
                Debug.WriteLine($"BackupManager.ExecuteJobsByNameAsync: Job execution completed");
            }
            else
            {
                Debug.WriteLine("BackupManager.ExecuteJobsByNameAsync: No jobs to launch");
            }
        }

        public bool RemoveJobByName(string jobName)
        {
            BackupJob jobToRemove = backupJobs.FirstOrDefault(j => j.Name == jobName);
            if (jobToRemove != null)
            {
                backupJobs.Remove(jobToRemove);
                SaveJobs(); // Save job configuration
                return true;
            }
            return false; // Ensure all code paths return a value
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

                                // Create the job using the parallel manager's semaphore
                                var job = new BackupJob(jobData.Name, sourcePath, targetPath, jobData.Type, this, _parallelManager.LargeFileTransferSemaphore);

                                backupJobs.Add(job);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading jobs from file: {ex.Message}. Consider a more robust error notification mechanism.");
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
                Debug.WriteLine($"Error saving jobs to file: {ex.Message}. Consider a more robust error notification mechanism.");
            }
        }

        /// <summary>
        /// Checks if there are any pending files with priority extensions in the backup jobs
        /// </summary>
        public bool HasAnyPendingPriorityFiles()
        {
            lock (_priorityLock)
            {
                return _parallelManager.HasAnyPendingPriorityFiles();
            }
        }

        public void Shutdown()
        {
            _businessMonitor.StopMonitoring();
            _parallelManager.Shutdown();
            _eventManager.RemoveListener(this);

            // Stop the remote control server if it's running
            _remoteControlService?.Shutdown();
        }

        // IEventListener implementation
        public void OnJobStatusChanged(JobStatus status)
        {
            // No action needed - this is primarily for remote control notifications
        }

        public void OnBusinessSoftwareStateChanged(bool isRunning)
        {
            // The BusinessApplicationMonitor now handles pausing/resuming jobs
            // This method remains for compatibility with IEventListener
            Debug.WriteLine($"BackupManager.OnBusinessSoftwareStateChanged: Business software running state is now {isRunning}");
        }

        /// <summary>
        /// Handles the event when a launch request is made for specific jobs
        /// </summary>
        /// <param name="jobNames">List of job names to launch</param>
        public async void OnLaunchJobsRequested(List<string> jobNames)
        {
            if (jobNames == null || !jobNames.Any()) return;

            Debug.WriteLine($"BackupManager.OnLaunchJobsRequested: Processing launch request for jobs: {string.Join(", ", jobNames)}");

            // Check if business software is running first
            if (_businessMonitor != null && _businessMonitor.IsRunning())
            {
                Debug.WriteLine("BackupManager.OnLaunchJobsRequested: Cannot launch jobs while business software is running");
                return;
            }

            // Reset job status for each job before executing
            foreach (var name in jobNames)
            {
                var job = backupJobs.FirstOrDefault(j => j.Name == name);
                if (job != null)
                {
                    job.Status.ResetForRun();
                    Debug.WriteLine($"BackupManager.OnLaunchJobsRequested: Reset job '{name}' status for execution");
                }
            }

            // Execute the jobs
            await ExecuteJobsByNameAsync(jobNames);
        }

        /// <summary>
        /// Handles the event when a pause request is made for specific jobs
        /// </summary>
        /// <param name="jobNames">List of job names to pause</param>
        public void OnPauseJobsRequested(List<string> jobNames)
        {
            if (jobNames == null || !jobNames.Any()) return;

            foreach (var name in jobNames)
            {
                var job = backupJobs.FirstOrDefault(j => j.Name == name);
                if (job != null && job.Status.State == BackupState.Running)
                {
                    job.Pause();
                    Debug.WriteLine($"BackupManager.OnPauseJobsRequested: Job '{name}' paused.");
                }
                else
                {
                    Debug.WriteLine($"BackupManager.OnPauseJobsRequested: Job '{name}' not found or not in running state.");
                }
            }
        }

        /// <summary>
        /// Handles the event when a resume request is made for specific jobs
        /// </summary>
        /// <param name="jobNames">List of job names to resume</param>
        public void OnResumeJobsRequested(List<string> jobNames)
        {
            if (jobNames == null || !jobNames.Any()) return;

            // Check if business software is running first
            if (_businessMonitor != null && _businessMonitor.IsRunning())
            {
                Debug.WriteLine("BackupManager.OnResumeJobsRequested: Cannot resume jobs while business software is running");
                return;
            }

            foreach (var name in jobNames)
            {
                var job = backupJobs.FirstOrDefault(j => j.Name == name);
                if (job != null && job.Status.State == BackupState.Paused)
                {
                    job.Resume();
                    Debug.WriteLine($"BackupManager.OnResumeJobsRequested: Job '{name}' resumed.");
                }
                else
                {
                    Debug.WriteLine($"BackupManager.OnResumeJobsRequested: Job '{name}' not found or not in paused state.");
                }
            }
        }

        /// <summary>
        /// Handles the event when a stop request is made for specific jobs
        /// </summary>
        /// <param name="jobNames">List of job names to stop</param>
        public void OnStopJobsRequested(List<string> jobNames)
        {
            if (jobNames == null || !jobNames.Any()) return;

            foreach (var name in jobNames)
            {
                var job = backupJobs.FirstOrDefault(j => j.Name == name);
                if (job != null && (job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused))
                {
                    job.Stop();
                    Debug.WriteLine($"BackupManager.OnStopJobsRequested: Job '{name}' stopped.");
                }
                else
                {
                    Debug.WriteLine($"BackupManager.OnStopJobsRequested: Job '{name}' not found or not in a stoppable state.");
                }
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

        /// <summary>
        /// Handles the event when the business application starts
        /// </summary>
        private void BusinessMonitor_BusinessAppStarted(object sender, EventArgs e)
        {
            Debug.WriteLine("BackupManager: Business application started event received");
            _eventManager.NotifyBusinessSoftwareStateChanged(true);
        }

        /// <summary>
        /// Handles the event when the business application stops
        /// </summary>
        private void BusinessMonitor_BusinessAppStopped(object sender, EventArgs e)
        {
            Debug.WriteLine("BackupManager: Business application stopped event received");
            _eventManager.NotifyBusinessSoftwareStateChanged(false);
        }

        /// <summary>
        /// Unregisters a job from the business application monitor
        /// </summary>
        /// <param name="job">The job to unregister</param>
        public void UnregisterJobFromBusinessMonitor(BackupJob job)
        {
            if (job != null)
            {
                _businessMonitor.UnregisterJob(job);
            }
        }

        /// <summary>
        /// Starts the remote control server to allow remote monitoring and control of backup jobs
        /// </summary>
        /// <param name="port">Port to listen on (default: 9000)</param>
        /// <returns>True if server started successfully</returns>
        public bool StartRemoteControlServer(int port = 9000)
        {
            // Create the remote control service if needed
            if (_remoteControlService == null)
            {
                _remoteControlService = new Services.RemoteControlService(this);
            }

            // Set the port
            _remoteControlService.ServerPort = port;

            // Start the server
            return _remoteControlService.StartServer();
        }

        /// <summary>
        /// Stops the remote control server
        /// </summary>
        public void StopRemoteControlServer()
        {
            _remoteControlService?.StopServer();
        }

        /// <summary>
        /// Gets whether the remote control server is running
        /// </summary>
        public bool IsRemoteControlServerRunning => _remoteControlService?.IsServerRunning ?? false;
    }
}
