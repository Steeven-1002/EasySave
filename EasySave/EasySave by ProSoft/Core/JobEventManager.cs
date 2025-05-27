using EasySave_by_ProSoft.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace EasySave_by_ProSoft.Core
{
    /// <summary>
    /// Centralized event manager for job state updates and persistence
    /// </summary>
    public class JobEventManager
    {
        private static JobEventManager _instance;
        private static readonly object _lockObject = new object();
        
        private readonly List<JobEventListeners> _listeners = new List<JobEventListeners>();
        private readonly string _stateFilePath;

        // Event for job status updates (for remote monitoring integration)
        public event EventHandler<JobStatus> JobStatusUpdated;

        public static JobEventManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new JobEventManager();
                    }
                }
                return _instance;
            }
        }

        private JobEventManager()
        {
            // Initialize state file path
            _stateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                "state.json"
            );

            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
        }

        public void AddListener(JobEventListeners listener)
        {
            lock (_listeners)
            {
                if (listener != null && !_listeners.Contains(listener))
                {
                    _listeners.Add(listener);
                    Debug.WriteLine($"JobEventManager: Added listener of type {listener.GetType().Name}");
                }
            }
        }

        public void RemoveListener(JobEventListeners listener)
        {
            lock (_listeners)
            {
                if (listener != null && _listeners.Remove(listener))
                {
                    Debug.WriteLine($"JobEventManager: Removed listener of type {listener.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// Notifies listeners about job status updates and persists job states to file
        /// </summary>
        /// <param name="jobStatus">Updated job status</param>
        public void NotifyListeners(JobStatus jobStatus)
        {
            if (jobStatus == null)
                return;

            try
            {
                // Create a snapshot of the job state for persistence
                var jobState = jobStatus.CreateSnapshot();
                
                // Persist job states to file
                SaveJobState(jobState);
                
                // Notify registered listeners
                NotifyJobStatusChanged(jobStatus);
                
                // Raise the event for remote monitoring
                RaiseJobStatusUpdated(jobStatus);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JobEventManager: Error notifying listeners: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies registered listeners about job status changes
        /// </summary>
        private void NotifyJobStatusChanged(JobStatus jobStatus)
        {
            lock (_listeners)
            {
                foreach (var listener in _listeners.ToList())
                {
                    try
                    {
                        listener.Update(
                            jobStatus.BackupJob?.Name ?? string.Empty,
                            jobStatus.State,
                            jobStatus.TotalFiles,
                            jobStatus.TotalSize,
                            jobStatus.RemainingFiles,
                            jobStatus.RemainingSize,
                            jobStatus.CurrentSourceFile,
                            jobStatus.CurrentTargetFile,
                            jobStatus.ElapsedTime.TotalMilliseconds,
                            jobStatus.EncryptionTimeMs,
                            jobStatus.Details
                        );
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"JobEventManager: Error notifying listener {listener.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Raises the JobStatusUpdated event for remote monitoring integration
        /// </summary>
        private void RaiseJobStatusUpdated(JobStatus jobStatus)
        {
            JobStatusUpdated?.Invoke(this, jobStatus);
        }

        /// <summary>
        /// Saves job state to the state file
        /// </summary>
        private void SaveJobState(JobState jobState)
        {
            try
            {
                // Read existing states
                var existingStates = ReadAllJobStates();
                
                // Remove existing state for this job and add the new one
                existingStates.RemoveAll(s => s.JobName == jobState.JobName);
                existingStates.Add(jobState);
                
                // Serialize and save
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(existingStates, options);
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JobEventManager: Error saving job state: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads all job states from the state file
        /// </summary>
        private List<JobState> ReadAllJobStates()
        {
            if (!File.Exists(_stateFilePath))
                return new List<JobState>();

            try
            {
                string json = File.ReadAllText(_stateFilePath);
                if (string.IsNullOrEmpty(json))
                    return new List<JobState>();

                var states = JsonSerializer.Deserialize<List<JobState>>(json);
                return states ?? new List<JobState>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JobEventManager: Error reading job states: {ex.Message}");
                return new List<JobState>();
            }
        }

        /// <summary>
        /// Gets all job states from the state file
        /// </summary>
        public List<JobState> GetAllJobStates()
        {
            return ReadAllJobStates();
        }
    }

    /// <summary>
    /// Interface for objects that listen to job events
    /// </summary>
    public interface JobEventListeners
    {
        void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile, double transfertDuration, double encryptionTimeMs, string details = null);
    }
}