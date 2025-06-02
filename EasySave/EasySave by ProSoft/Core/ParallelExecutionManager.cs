using EasySave_by_ProSoft.Models;
using System.Diagnostics;
using System.IO;

namespace EasySave_by_ProSoft.Core
{
    /// <summary>
    /// Manages parallel execution of backup jobs with priority handling and resource management
    /// </summary>
    public class ParallelExecutionManager
    {
        private readonly List<BackupJob> _activeJobs = new();
        private readonly Dictionary<BackupJob, Task> _jobTasks = new();
        private readonly object _largeLock = new();
        private readonly SemaphoreSlim _largeFileTransferSemaphore;
        private readonly CancellationTokenSource _cts = new();

        public ParallelExecutionManager()
        {
            // Initialize the semaphore for large file transfers
            int maxConcurrentLargeFileTransfers = 1; // Can be configured from settings
            _largeFileTransferSemaphore = new SemaphoreSlim(maxConcurrentLargeFileTransfers, maxConcurrentLargeFileTransfers);
        }

        public SemaphoreSlim LargeFileTransferSemaphore => _largeFileTransferSemaphore;

        public async Task ExecuteJobsInParallelAsync(List<BackupJob> jobs)
        {
            if (jobs == null || !jobs.Any()) return;

            Debug.WriteLine($"ParallelExecutionManager: Starting execution of {jobs.Count} jobs");
            List<Task> runningTasks = new();

            foreach (var job in jobs)
            {
                lock (_activeJobs)
                {
                    if (!_activeJobs.Contains(job))
                    {
                        _activeJobs.Add(job);
                        Debug.WriteLine($"ParallelExecutionManager: Added job '{job.Name}' to active jobs list");

                        var jobTask = Task.Run(() =>
                        {
                            try
                            {
                                Debug.WriteLine($"ParallelExecutionManager: Starting job '{job.Name}'");
                                job.Start();
                                Debug.WriteLine($"ParallelExecutionManager: Job '{job.Name}' completed with status: {job.Status.State}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"ParallelExecutionManager: Error executing job {job.Name}: {ex.Message}");
                                job.Status.SetError($"Error: {ex.Message}");
                            }
                            finally
                            {
                                lock (_activeJobs)
                                {
                                    _activeJobs.Remove(job);
                                    Debug.WriteLine($"ParallelExecutionManager: Removed job '{job.Name}' from active jobs list");
                                }
                            }
                        }, _cts.Token);

                        _jobTasks[job] = jobTask;
                        runningTasks.Add(jobTask);
                    }
                    else
                    {
                        Debug.WriteLine($"ParallelExecutionManager: Job '{job.Name}' is already in active jobs list, skipping");
                    }
                }
            }

            if (runningTasks.Any())
            {
                Debug.WriteLine($"ParallelExecutionManager: Waiting for {runningTasks.Count} jobs to complete");
                await Task.WhenAll(runningTasks);
                Debug.WriteLine("ParallelExecutionManager: All jobs completed");
            }
            else
            {
                Debug.WriteLine("ParallelExecutionManager: No jobs to run");
            }
        }

        public bool HasAnyPendingPriorityFiles()
        {
            lock (_activeJobs)
            {
                return _activeJobs.Any(job =>
                    job.GetPendingFiles().Any(file =>
                        IsPriorityFile(file)));
            }
        }

        public bool IsPriorityFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return PriorityExtensionManager.IsPriorityExtension(extension);
        }

        public bool CanTransferLargeFile(long fileSize)
        {
            // Check if the file size exceeds the threshold
            double thresholdKBSetting = AppSettings.Instance.GetSetting("LargeFileSizeThresholdKey") as double?
                ?? AppSettings.Instance.GetSetting("DefaultLargeFileSizeThresholdKey") as double? ?? 100.0;

            long largeFileSizeThresholdBytes = (long)(thresholdKBSetting * 1024);

            return fileSize <= largeFileSizeThresholdBytes || _largeFileTransferSemaphore.CurrentCount > 0;
        }

        public void PauseNonPriorityJobs()
        {
            lock (_activeJobs)
            {
                foreach (var job in _activeJobs)
                {
                    if (job.Status.State == BackupState.Running && !HasPriorityFiles(job))
                    {
                        job.Pause();
                    }
                }
            }
        }

        public void ResumeNonPriorityJobs()
        {
            lock (_activeJobs)
            {
                foreach (var job in _activeJobs)
                {
                    if (job.Status.State == BackupState.Paused)
                    {
                        job.Resume();
                    }
                }
            }
        }

        private bool HasPriorityFiles(BackupJob job)
        {
            return job.GetPendingFiles().Any(file => IsPriorityFile(file));
        }

        public void Shutdown()
        {
            _cts.Cancel();
        }
    }
}