using System.Text.Json;
using EasySave.Core.Models;
using EasySave.Interfaces;

namespace EasySave.Core
{
    // Manages the state of backup jobs and provides functionality to save, load, and update job states.
    // Implements the IStateObserver interface.
    public class StateManager : IStateObserver
    {
        private static StateManager? _instance;
        private readonly string _stateFilePath;
        private List<JobState> _jobStates;

        // Initializes a new instance of the StateManager class.
        // stateFilePath: The file path where the state will be saved and loaded from.
        public StateManager(string stateFilePath)
        {
            _stateFilePath = stateFilePath;
            _jobStates = new List<JobState>();
            LoadState();
        }

        // Gets the singleton instance of the StateManager class.
        public static StateManager Instance
        {
            get
            {
                return _instance ??= new StateManager(ReferenceEquals(ConfigManager.Instance.StateFilePath, "")
                    ? "state.json"
                    : ConfigManager.Instance.StateFilePath);
            }
        }

        // Updates the state of a specific job and saves the updated state to the file.
        // jobName: The name of the job.
        // newState: The new state of the job.
        // totalFiles: The total number of files in the job.
        // totalSize: The total size of files in the job.
        // remainingFiles: The number of remaining files to process.
        // remainingSize: The size of remaining files to process.
        // currentSourceFile: The current source file being processed.
        // currentTargetFile: The current target file being processed.
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


        // Initializes the state of a job with the given total files and size.
        // jobName: The name of the job.
        // totalFiles: The total number of files in the job.
        // totalSize: The total size of files in the job.
        public void InitializeJobState(string jobName, int totalFiles, long totalSize)
        {
            StateChanged(jobName, BackupState.ACTIVE, totalFiles, totalSize, totalFiles, totalSize, "Starting scan...", "");
        }


        // Finalizes the state of a job with the specified final state.
        // jobName: The name of the job.
        // finalState: The final state of the job.
        public void FinalizeJobState(string jobName, BackupState finalState)
        {
            StateChanged(jobName, finalState, 0, 0, 0, 0, "Finalized", "");
        }


        // Saves the current state of all jobs to the state file.
        private void SaveState()
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


        // Loads the state of all jobs from the state file.
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


        // Retrieves the state of a specific job by its name.
        // jobName: The name of the job.
        // Returns the JobState of the job, or null if not found.
        public JobState? GetState(string jobName)
        {
            return _jobStates.FirstOrDefault(js => js.JobName == jobName);
        }
    }
}