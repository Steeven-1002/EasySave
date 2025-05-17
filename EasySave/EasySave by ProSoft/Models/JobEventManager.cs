using System;
using System.Collections.Generic;
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
        // List of different listeners observing job state changes
        private List<JobEventListeners> listeners = new List<JobEventListeners>();
        
        // Path to the state.json file maintained in real time
        private readonly string stateFilePath;

        /// <summary>
        /// Initializes a new instance of the job event manager
        /// </summary>
        public JobEventManager() 
        {
            // Define the state.json file location
            stateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
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
        public void NotifyListeners(ref JobStatus jobStatus)
        {
            // Update the state.json file in real time
            UpdateStateFile(jobStatus);

            // Notify all listeners (logs, interfaces, etc.)
            foreach (var listener in listeners)
            {
                listener.Update(
                    jobStatus.ExecutionId.ToString(),
                    jobStatus.State,
                    jobStatus.TotalFiles,
                    jobStatus.TotalSize,
                    jobStatus.RemainingFiles,
                    jobStatus.RemainingSize,
                    jobStatus.CurrentSourceFile,
                    jobStatus.CurrentTargetFIle,
                    jobStatus.TransferRate
                );
            }
        }
        
        /// <summary>
        /// Updates the state.json file with the current job state
        /// </summary>
        /// <param name="jobStatus">The job status to save</param>
        private void UpdateStateFile(JobStatus jobStatus)
        {
            try
            {
                // Create a snapshot of the job for serialization
                var snapshot = jobStatus.CreateSnapshot();
                
                // Load existing states, if any
                List<JobState> allJobStates = new List<JobState>();
                if (File.Exists(stateFilePath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(stateFilePath);
                        if (!string.IsNullOrEmpty(jsonContent))
                        {
                            allJobStates = JsonSerializer.Deserialize<List<JobState>>(jsonContent) ?? new List<JobState>();
                        }
                    }
                    catch (JsonException)
                    {
                        // In case of deserialization error, start with an empty list
                        allJobStates = new List<JobState>();
                    }
                }
                
                // Update the current job state or add it if it doesn't exist
                bool jobFound = false;
                for (int i = 0; i < allJobStates.Count; i++)
                {
                    if (allJobStates[i].JobName == snapshot.JobName)
                    {
                        allJobStates[i] = snapshot;
                        jobFound = true;
                        break;
                    }
                }
                
                if (!jobFound)
                {
                    allJobStates.Add(snapshot);
                }
                
                // Serialization options for readable JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                
                // Serialize and save to file
                string serializedData = JsonSerializer.Serialize(allJobStates, options);
                File.WriteAllText(stateFilePath, serializedData.Replace("},", "},\n")); // Add newlines for readability
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating state.json file: {ex.Message}");
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
                    string jsonContent = File.ReadAllText(stateFilePath);
                    return JsonSerializer.Deserialize<List<JobState>>(jsonContent) ?? new List<JobState>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading state.json file: {ex.Message}");
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
                Console.WriteLine($"Error cleaning up state.json file: {ex.Message}");
            }
        }
    }
}
