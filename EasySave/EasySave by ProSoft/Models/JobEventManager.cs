using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Implements Observer pattern responsible for communicating job state changes
    /// to different listeners (logs, state.json file, MVVM view)
    /// </summary>
    public class JobEventManager
    {
        private static readonly Lazy<JobEventManager> instance = new Lazy<JobEventManager>(() => new JobEventManager());
        public static JobEventManager Instance => instance.Value;

        // List of different listeners observing job state changes
        private List<JobEventListeners> listeners = new List<JobEventListeners>();

        // Path to the state.json file maintained in real time
        private readonly string stateFilePath;

        /// <summary>
        /// Initializes a new instance of the job event manager
        /// </summary>
        private JobEventManager()
        {
            // Define the state.json file location
            stateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                "state.json"
            );

            // Create directory if it doesn't exist
            string directoryPath = Path.GetDirectoryName(stateFilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        /// <summary>
        /// Adds a listener to be notified of job state changes
        /// </summary>
        /// <param name="listener">The listener to add</param>
        public void AddListener(JobEventListeners listener)
        {
            if (listener != null && !listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        /// <summary>
        /// Removes a listener from the observer list
        /// </summary>
        /// <param name="listener">The listener to remove</param>
        public void RemoveListener(JobEventListeners listener)
        {
            listeners.Remove(listener);
        }

        /// <summary>
        /// Notifies all listeners of a job state change and updates the state.json file
        /// </summary>
        /// <param name="jobStatus">The job status that was modified</param>
        public void NotifyListeners(JobStatus jobStatus)
        {
            try
            {
                // Update the state.json file in real time
                UpdateStateFile(jobStatus);

                // Use Dispatcher to ensure updates happen on UI thread if needed
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => NotifyListenersInternal(jobStatus));
                }
                else
                {
                    NotifyListenersInternal(jobStatus);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error notifying listeners: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Internal method to notify all listeners
        /// </summary>
        private void NotifyListenersInternal(JobStatus jobStatus)
        {
            // Make a copy of the listeners collection to avoid modification during iteration
            var listenersCopy = new List<JobEventListeners>(listeners);
            
            // Notify all listeners (logs, interfaces, etc.)
            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener.Update(
                        jobStatus.BackupJob.Name,
                        jobStatus.State,
                        jobStatus.TotalFiles,
                        jobStatus.TotalSize,
                        jobStatus.RemainingFiles,
                        jobStatus.RemainingSize,
                        jobStatus.CurrentSourceFile,
                        jobStatus.CurrentTargetFile,
                        jobStatus.TransferRate,
                        jobStatus.EncryptionTimeMs,
                        jobStatus.Details
                    );
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show($"Error updating listener: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Updates the state.json file with the current job state
        /// </summary>
        /// <param name="jobStatus">The job status to save</param>
        private void UpdateStateFile(JobStatus jobStatus)
        {
            if (jobStatus?.BackupJob == null || string.IsNullOrEmpty(jobStatus.BackupJob.Name))
            {
                // Cannot serialize without a valid job name
                return;
            }

            try
            {
                // Use file locking to prevent concurrent access issues
                using (FileStream fileStream = new FileStream(stateFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    // Create a snapshot of the job for serialization
                    var snapshot = jobStatus.CreateSnapshot();

                    // Load existing states, if any
                    List<JobState> allJobStates = new List<JobState>();
                    if (fileStream.Length > 0)
                    {
                        try
                        {
                            // Read existing JSON content
                            fileStream.Position = 0;
                            using (StreamReader reader = new StreamReader(fileStream, leaveOpen: true))
                            {
                                string jsonContent = reader.ReadToEnd();
                                if (!string.IsNullOrEmpty(jsonContent))
                                {

                                    allJobStates = JsonSerializer.Deserialize<List<JobState>>(jsonContent) ?? new List<JobState>();

                                    // Ensure proper state for each job
                                    foreach (var state in allJobStates)
                                    {
                                        // Convert any Initialise states to their proper representation
                                        if (state.State == BackupState.Initialise && !string.IsNullOrEmpty(state.StateAsString))
                                        {
                                            state.State = JobState.ConvertStringToState(state.StateAsString);
                                        }
                                    }

                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // In case of deserialization error, start with an empty list
                            System.Windows.Forms.MessageBox.Show("Error reading state.json file. Starting with an empty list.");
                            allJobStates = new List<JobState>();
                        }
                    }

                    // Update the current job state or add it if it doesn't exist
                    bool jobFound = false;
                    for (int i = 0; i < allJobStates.Count; i++)
                    {
                        if (allJobStates[i].JobName == snapshot.JobName)
                        {
                            // Preserve processed files from previous state if needed for resume capability
                            if (snapshot.State == BackupState.Running &&
                                allJobStates[i].ProcessedFiles?.Count > 0 &&
                                snapshot.ProcessedFiles?.Count == 0)
                            {
                                snapshot.ProcessedFiles = allJobStates[i].ProcessedFiles;
                            }

                            // Copy updated job state but be careful not to reset important fields
                            if (snapshot.State == BackupState.Initialise)
                            {
                                // Keep the existing state if the new state is Initialise
                                // This preserves the previous state during initialization
                                snapshot.State = allJobStates[i].State;
                            }

                            allJobStates[i] = snapshot;
                            jobFound = true;
                            break;
                        }
                    }

                    if (!jobFound)
                    {
                        // Validate state before adding a new job
                        if (snapshot.State == BackupState.Initialise)
                        {
                            snapshot.State = BackupState.Waiting;
                        }
                        allJobStates.Add(snapshot);
                    }
                    else
                    {
                        // BUGFIX: Ensure other jobs' states aren't affected by this update
                        // For each job that isn't the current one being updated, check if we need to preserve its state
                        // This prevents other jobs from being incorrectly set to WAITING during another job's execution
                        for (int i = 0; i < allJobStates.Count; i++)
                        {
                            // Skip the job we just updated
                            if (allJobStates[i].JobName == snapshot.JobName)
                                continue;

                            // Don't modify jobs that are in a final state (Completed or Error)
                            if (allJobStates[i].State == BackupState.Completed ||
                                allJobStates[i].State == BackupState.Error)
                                continue;

                            // Preserve the state of Running or Paused jobs instead of resetting them to Waiting
                            if (allJobStates[i].State == BackupState.Initialise)
                            {
                                // If a job is in Initialise state, set it to Waiting (this is safe)
                                allJobStates[i].State = BackupState.Waiting;
                            }

                            // Otherwise leave the job in its current state (Running, Paused, or Waiting)
                        }
                    }

                    // Serialization options for JSON format
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    // Clear file content and write updated JSON
                    fileStream.SetLength(0);
                    fileStream.Position = 0;
                    using (StreamWriter writer = new StreamWriter(fileStream))
                    {
                        string serializedData = JsonSerializer.Serialize(allJobStates, options);
                        writer.Write(serializedData);
                        writer.Flush();
                    }
                }
            }
            catch (IOException ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error writing to state.json file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                // File might be locked by another process, try again later
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Unexpected error: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                // Don't propagate error to avoid disrupting main flow
            }
        }

        /// <summary>
        /// Retrieves all job states from the state.json file
        /// </summary>
        /// <returns>List of job states</returns>
        public List<JobState> GetAllJobStates()
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    // Use a file lock to prevent reading while file is being written
                    using (FileStream fileStream = new FileStream(stateFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        string jsonContent = reader.ReadToEnd();
                        if (string.IsNullOrEmpty(jsonContent))
                        {
                            return new List<JobState>();
                        }

                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        return JsonSerializer.Deserialize<List<JobState>>(jsonContent, options) ?? new List<JobState>();
                    }
                }
            }
            catch (JsonException ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error deserializing state.json file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                // File might be corrupted, consider backing it up and creating a new one
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Unexpected error: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

            return new List<JobState>();
        }

        /// <summary>
        /// Cleans up the state.json file by removing completed or error job states
        /// </summary>
        public void CleanupCompletedJobs()
        {
            try
            {
                List<JobState> states = GetAllJobStates();
                List<JobState> activeStates = states.FindAll(s =>
                    s.State != BackupState.Completed && s.State != BackupState.Error);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string serializedData = JsonSerializer.Serialize(activeStates, options);
                File.WriteAllText(stateFilePath, serializedData);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Unexpected error: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}
