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

        [JsonIgnore]
        private string stateStringValue;

        [JsonPropertyName("State")]
        public string StateAsString
        {
            get => stateStringValue ?? ConvertStateToString(State);
            set
            {
                stateStringValue = value;
                State = ConvertStringToState(value);
            }
        }

        [JsonIgnore]
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

        [JsonIgnore]
        public string Details { get; set; } = string.Empty;

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
            Details = jobStatus.Details ?? string.Empty;
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
                    System.Windows.Forms.MessageBox.Show("State file path is not set.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                System.Windows.Forms.MessageBox.Show($"Error serializing state to file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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
                System.Windows.Forms.MessageBox.Show($"Error loading state from file: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

            return null;
        }

        /// <summary>
        /// Creates a snapshot of the current job state
        /// </summary>
        /// <returns>A new JobState instance representing the current state</returns>
        public JobState CreateSnapshot()
        {
            var snapshot = new JobState
            {
                JobName = this.JobName,
                SourcePath = this.SourcePath,
                TargetPath = this.TargetPath,
                Type = this.Type,
                Timestamp = DateTime.Now,
                State = this.State,
                TotalFiles = this.TotalFiles,
                TotalSize = this.TotalSize,
                RemainingFiles = this.RemainingFiles,
                RemainingSize = this.RemainingSize,
                CurrentSourceFile = this.CurrentSourceFile ?? string.Empty,
                CurrentTargetFile = this.CurrentTargetFile ?? string.Empty,
                StartTime = this.StartTime,
                EndTime = this.EndTime,
                TransferRate = this.TransferRate,
                ProgressPercentage = this.ProgressPercentage,
                ExecutionId = this.ExecutionId,
                EncryptionTimeMs = this.EncryptionTimeMs,
                ProcessedFiles = new List<string>(this.ProcessedFiles),
                Details = this.Details ?? string.Empty
            };

            return snapshot;
        }
    }
}
