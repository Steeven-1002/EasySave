using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network;
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
        private readonly NetworkMonitor _networkMonitor;
        private readonly object _largeLock = new();
        private readonly SemaphoreSlim _largeFileTransferSemaphore;
        private readonly CancellationTokenSource _cts = new();

        public ParallelExecutionManager()
        {
            _networkMonitor = new NetworkMonitor();
            _networkMonitor.StartMonitoring();

            // Initialize the semaphore for large file transfers
            int maxConcurrentLargeFileTransfers = 1; // Can be configured from settings
            _largeFileTransferSemaphore = new SemaphoreSlim(maxConcurrentLargeFileTransfers, maxConcurrentLargeFileTransfers);
        }

        public SemaphoreSlim LargeFileTransferSemaphore => _largeFileTransferSemaphore;

        public async Task ExecuteJobsInParallelAsync(List<BackupJob> jobs)
        {
            if (jobs == null || !jobs.Any()) return;

            List<Task> runningTasks = new();

            foreach (var job in jobs)
            {
                lock (_activeJobs)
                {
                    if (!_activeJobs.Contains(job))
                    {
                        _activeJobs.Add(job);

                        var jobTask = Task.Run(() =>
                        {
                            try
                            {
                                job.Start();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error executing job {job.Name}: {ex.Message}");
                                job.Status.SetError($"Error: {ex.Message}");
                            }
                            finally
                            {
                                lock (_activeJobs)
                                {
                                    _activeJobs.Remove(job);
                                }
                            }
                        }, _cts.Token);

                        _jobTasks[job] = jobTask;
                        runningTasks.Add(jobTask);
                    }
                }
            }

            if (runningTasks.Any())
            {
                await Task.WhenAll(runningTasks);
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

        public void ManageNetworkLoad(int thresholdKBps)
        {
            if (_networkMonitor.IsBandwidthExceededThreshold(thresholdKBps))
            {
                PauseNonPriorityJobs();
            }
            else
            {
                ResumeNonPriorityJobs();
            }
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
            _networkMonitor.StopMonitoring();
            _cts.Cancel();
        }
    }
}