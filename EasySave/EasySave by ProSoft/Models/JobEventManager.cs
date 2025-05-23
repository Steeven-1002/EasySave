using System.Diagnostics;
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
        private static readonly object _stateFileLock = new object();

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
            if (jobStatus == null)
            {
                Debug.WriteLine("JobEventManager.NotifyListeners: jobStatus is null. Aborting notification.");
                return;
            }
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
                Debug.WriteLine("JobEventManager.UpdateStateFile: Cannot update state file due to null jobStatus, BackupJob, or empty JobName.");
                return;
            }

            lock (_stateFileLock)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Debug.WriteLine($"Thread-{threadId}: Attempting to update state.json for job '{jobStatus.BackupJob.Name}', Status: {jobStatus.State}");
                try
                {
                    var snapshot = jobStatus.CreateSnapshot();
                    List<JobState> allJobStates;

                    if (File.Exists(stateFilePath))
                    {
                        string jsonContent = File.ReadAllText(stateFilePath);
                        allJobStates = string.IsNullOrWhiteSpace(jsonContent) ? new List<JobState>() : JsonSerializer.Deserialize<List<JobState>>(jsonContent) ?? new List<JobState>();
                    }
                    else
                    {
                        allJobStates = new List<JobState>();
                    }

                    // Update or add current job status
                    int existingStateIndex = allJobStates.FindIndex(s => s.JobName == snapshot.JobName);
                    if (existingStateIndex != -1)
                    {
                        allJobStates[existingStateIndex] = snapshot; // Replace the old state
                    }
                    else
                    {
                        allJobStates.Add(snapshot);
                    }
                    for (int i = 0; i < allJobStates.Count; i++)
                    {
                        if (allJobStates[i].JobName == snapshot.JobName) continue;
                        if (allJobStates[i].State == BackupState.Completed || allJobStates[i].State == BackupState.Error) continue;
                        if (allJobStates[i].State == BackupState.Initialise)
                        {
                            allJobStates[i].State = BackupState.Waiting;
                        }
                    }


                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };
                    string serializedData = JsonSerializer.Serialize(allJobStates, options);
                    File.WriteAllText(stateFilePath, serializedData); // Overwrites the file with the new full list
                    Debug.WriteLine($"Thread-{threadId}: Successfully updated state.json for job '{jobStatus.BackupJob.Name}'.");

                }
                // The external lock is the main protection against concurrent access.
                catch (IOException ex)
                {
                    Debug.WriteLine($"Thread-{threadId}: IOException in UpdateStateFile for '{jobStatus.BackupJob.Name}': {ex.Message}. File: {stateFilePath}");
                    // System.Windows.Forms.MessageBox.Show($"Error writing to state.json file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"Thread-{threadId}: JsonException in UpdateStateFile for '{jobStatus.BackupJob.Name}': {ex.Message}. File content might be corrupt. File: {stateFilePath}");
                    // System.Windows.Forms.MessageBox.Show($"Error processing state.json (JSON format error): {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Thread-{threadId}: Unexpected error in UpdateStateFile for '{jobStatus.BackupJob.Name}': {ex.Message}. File: {stateFilePath}");
                    // System.Windows.Forms.MessageBox.Show($"Unexpected error updating state file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
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
