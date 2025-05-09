using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EasySave.Models;
using EasySave.Interfaces;
using EasySave.ConsoleApp;

namespace EasySave.Services
{
    /// <summary>
    /// Manages the state of backup jobs and provides functionality to save, load, and update job states.
    /// Implements the <see cref="IStateObserver"/> interface.
    /// </summary>
    public class StateManager : IStateObserver
    {
        private static StateManager? _instance;
        private readonly string _stateFilePath;
        private List<JobState> _jobStates;
        private readonly List<IStateObserver> _observers;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateManager"/> class.
        /// </summary>
        /// <param name="stateFilePath">The file path where the state will be saved and loaded from.</param>
        public StateManager(string stateFilePath)
        {
            _stateFilePath = stateFilePath;
            _jobStates = new List<JobState>();
            _observers = new List<IStateObserver>();
            LoadState();
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="StateManager"/> class.
        /// </summary>
        public static StateManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new StateManager(ReferenceEquals(ConfigManager.Instance.StateFilePath, "") ? "state.json" : ConfigManager.Instance.StateFilePath);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Updates the state of a specific job and saves the updated state to the file.
        /// </summary>
        /// <param name="jobName">The name of the job.</param>
        /// <param name="newState">The new state of the job.</param>
        /// <param name="totalFiles">The total number of files in the job.</param>
        /// <param name="totalSize">The total size of files in the job.</param>
        /// <param name="remainingFiles">The number of remaining files to process.</param>
        /// <param name="remainingSize">The size of remaining files to process.</param>
        /// <param name="currentSourceFile">The current source file being processed.</param>
        /// <param name="currentTargetFile">The current target file being processed.</param>
        public void StateChanged(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile)
        {
            JobState? jobState = _jobStates.FirstOrDefault(js => js.JobName == jobName);
            bool newEntry = false;
            if (jobState == null)
            {
                jobState = new JobState(jobName);
                newEntry = true;
            }

            jobState.Timestamp = DateTime.Now;
            jobState.State = Enum.GetName(typeof(BackupState), newState) ?? "UNKNOWN";
            jobState.TotalFiles = totalFiles;
            jobState.TotalSize = totalSize;
            jobState.RemainingFiles = remainingFiles;
            jobState.RemainingSize = remainingSize;
            jobState.CurrentSourceFile = currentSourceFile;
            jobState.CurrentTargetFile = currentTargetFile;

            if (newEntry) _jobStates.Add(jobState);

            Console.WriteLine($"StateManager: Updated state for job '{jobName}'. Current file: {currentSourceFile}");
            SaveState();
        }

        /// <summary>
        /// Initializes the state of a job with the given total files and size.
        /// </summary>
        /// <param name="jobName">The name of the job.</param>
        /// <param name="totalFiles">The total number of files in the job.</param>
        /// <param name="totalSize">The total size of files in the job.</param>
        public void InitializeJobState(string jobName, int totalFiles, long totalSize)
        {
            StateChanged(jobName, BackupState.ACTIVE, totalFiles, totalSize, totalFiles, totalSize, "Starting scan...", "");
        }

        /// <summary>
        /// Finalizes the state of a job with the specified final state.
        /// </summary>
        /// <param name="jobName">The name of the job.</param>
        /// <param name="finalState">The final state of the job.</param>
        public void FinalizeJobState(string jobName, BackupState finalState)
        {
            StateChanged(jobName, finalState, 0, 0, 0, 0, "Finalized", "");
        }

        /// <summary>
        /// Saves the current state of all jobs to the state file.
        /// </summary>
        public void SaveState()
        {
            try
            {
                string json = JsonSerializer.Serialize(_jobStates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StateManager ERROR saving state to '{_stateFilePath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the state of all jobs from the state file.
        /// </summary>
        public void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    string json = File.ReadAllText(_stateFilePath);
                    _jobStates = JsonSerializer.Deserialize<List<JobState>>(json) ?? new List<JobState>();
                }
                else
                {
                    _jobStates = new List<JobState>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StateManager ERROR loading state from '{_stateFilePath}': {ex.Message}");
                _jobStates = new List<JobState>();
            }
        }

        /// <summary>
        /// Retrieves the state of a specific job by its name.
        /// </summary>
        /// <param name="jobName">The name of the job.</param>
        /// <returns>The <see cref="JobState"/> of the job, or null if not found.</returns>
        public JobState? GetState(string jobName)
        {
            return _jobStates.FirstOrDefault(js => js.JobName == jobName);
        }
    }
}