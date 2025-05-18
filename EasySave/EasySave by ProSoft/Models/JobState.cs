using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Represents the serializable state of a backup job for persistence in state.json
    /// </summary>
    public class JobState
    {
        // Path to state file - not serialized
        [JsonIgnore]
        private string stateFilePath;

        // Core job identification properties - Use JsonPropertyName to match exact format
        [JsonPropertyName("Name")]
        public string JobName { get; set; } = string.Empty;

        [JsonIgnore] // Exclude from state.json
        public Guid ExecutionId { get; set; } = Guid.Empty;

        [JsonIgnore] // Exclude from state.json
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Source and target file paths currently being processed
        [JsonPropertyName("SourceFilePath")]
        public string CurrentSourceFile { get; set; } = string.Empty;

        [JsonPropertyName("TargetFilePath")]
        public string CurrentTargetFile { get; set; } = string.Empty;

        // Job status properties with correct JSON property names
        [JsonPropertyName("State")]
        public string StateAsString => ConvertStateToString(State);

        [JsonIgnore] // Use StateAsString for serialization instead
        public BackupState State { get; set; } = BackupState.Initialise;

        [JsonPropertyName("TotalFilesToCopy")]
        public int TotalFiles { get; set; }

        [JsonPropertyName("TotalFilesSize")]
        public long TotalSize { get; set; }

        [JsonPropertyName("NbFilesLeftToDo")]
        public int RemainingFiles { get; set; }

        [JsonPropertyName("Progression")]
        public double ProgressPercentage { get; set; }

        // Properties excluded from state.json format
        [JsonIgnore]
        public long RemainingSize { get; set; }

        [JsonIgnore]
        public DateTime StartTime { get; set; }

        [JsonIgnore]
        public DateTime? EndTime { get; set; }

        [JsonIgnore]
        public double TransferRate { get; set; }

        [JsonIgnore]
        public long EncryptionTimeMs { get; set; }

        [JsonIgnore]
        public string SourcePath { get; set; } = string.Empty;

        [JsonIgnore]
        public string TargetPath { get; set; } = string.Empty;

        [JsonIgnore]
        public BackupType Type { get; set; } = BackupType.Full;

        [JsonPropertyName("ProcessedFiles")]
        public List<string> ProcessedFiles { get; set; } = new List<string>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public JobState()
        {
            // Initialize with a proper state instead of Initialise
            State = BackupState.Waiting;
        }

        /// <summary>
        /// Converts BackupState enum to the string format required in state.json
        /// </summary>
        private string ConvertStateToString(BackupState state)
        {
            return state switch
            {
                BackupState.Waiting => "WAITING",
                BackupState.Running => "ACTIVE",
                BackupState.Paused => "PAUSED",
                BackupState.Completed => "END",
                BackupState.Error => "ERROR",
                BackupState.Initialise => "WAITING", // Changed from UNKNOWN to WAITING for Initialise state
                _ => "UNKNOWN" // Default to UNKNOWN
            };
        }

        /// <summary>
        /// Converts string state from state.json to BackupState enum
        /// </summary>
        /// <param name="stateString">String representation of state</param>
        /// <returns>Corresponding BackupState enum value</returns>
        public static BackupState ConvertStringToState(string stateString)
        {
            return stateString?.ToUpperInvariant() switch
            {
                "WAITING" => BackupState.Waiting,
                "ACTIVE" => BackupState.Running,
                "PAUSED" => BackupState.Paused,
                "END" => BackupState.Completed,
                "ERROR" => BackupState.Error,
                _ => BackupState.Initialise
            };
        }

        /// <summary>
        /// Updates this state from a JobStatus instance
        /// </summary>
        public void Update(ref JobStatus jobStatus)
        {
            Timestamp = DateTime.Now;
            State = jobStatus.State;
            TotalFiles = jobStatus.TotalFiles;
            TotalSize = jobStatus.TotalSize;
            RemainingFiles = jobStatus.RemainingFiles;
            RemainingSize = jobStatus.RemainingSize;
            CurrentSourceFile = jobStatus.CurrentSourceFile ?? string.Empty;
            CurrentTargetFile = jobStatus.CurrentTargetFile ?? string.Empty;
            StartTime = jobStatus.StartTime;
            EndTime = jobStatus.EndTime;
            TransferRate = jobStatus.TransferRate;
            ProgressPercentage = jobStatus.ProgressPercentage;
            ExecutionId = jobStatus.ExecutionId;
            EncryptionTimeMs = jobStatus.EncryptionTimeMs;
            ProcessedFiles = new List<string>(jobStatus.ProcessedFiles);
            SerializeStateToFile();
        }

        /// <summary>
        /// Serializes the current state to the state.json file
        /// </summary>
        private void SerializeStateToFile()
        {
            try
            {
                if (string.IsNullOrEmpty(stateFilePath))
                {
                    Console.WriteLine("Cannot serialize state: stateFilePath is not set");
                    return;
                }

                // Ensure directory exists
                string directoryPath = Path.GetDirectoryName(stateFilePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string json = JsonSerializer.Serialize(this, options);

                // Use FileStream for better control over flushing and file locking
                using (FileStream fs = new FileStream(stateFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    writer.Write(json);
                    writer.Flush();
                    fs.Flush(true); // Force flush to disk
                }
            }
            catch (Exception ex)
            {
                // Log error or handle exception
                Console.WriteLine($"Error serializing state to file: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a saved job state from state.json by job name
        /// </summary>
        /// <param name="jobName">The name of the job to find in state.json</param>
        /// <param name="statesFilePath">Path to the state.json file</param>
        /// <returns>The found JobState or null if not found</returns>
        public static JobState LoadFromStateFile(string jobName, string statesFilePath)
        {
            try
            {
                if (File.Exists(statesFilePath))
                {
                    // Read all states from file
                    string jsonContent = File.ReadAllText(statesFilePath);
                    if (string.IsNullOrEmpty(jsonContent))
                        return null;

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var allStates = JsonSerializer.Deserialize<List<JobState>>(jsonContent, options);

                    System.Diagnostics.Debug.WriteLine($"Loaded {allStates?.Count} states from {statesFilePath}");

                    if (allStates == null)
                        return null;

                    // Find the state for the specified job
                    var savedState = allStates.Find(s => s.JobName.Equals(jobName, StringComparison.OrdinalIgnoreCase));
                    if (savedState != null)
                    {
                        // Set the state file path for the loaded state
                        savedState.stateFilePath = statesFilePath;

                        // Convert the string state to enum if needed
                        if (savedState.State == BackupState.Initialise && !string.IsNullOrEmpty(savedState.StateAsString))
                        {
                            savedState.State = ConvertStringToState(savedState.StateAsString);
                        }

                        return savedState;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading state from file for job {jobName}: {ex.Message}");
            }

            return null;
        }
    }
}
