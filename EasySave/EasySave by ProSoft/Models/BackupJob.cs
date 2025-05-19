using EasySave_by_ProSoft.Localization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Represents a backup job that manages the entire backup operation
    /// </summary>
    public class BackupJob
    {
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public BackupType Type { get; set; }
        public JobStatus Status { get; set; }

        private BusinessApplicationMonitor _businessMonitor;
        private EncryptionService _encryptionService;
        private IBackupFileStrategy _backupFileStrategy;
        private BackupManager _backupManager;
        private long _encryptionTime = 0;
        public long TotalEncryptionTime => _encryptionTime; // Add this property for logging
        private bool _isRunning = false;
        private bool _isPaused = false;
        private bool _stopRequested = false;
        private List<string> toProcessFiles = new List<string>();

        /// <summary>
        /// Initializes a new backup job
        /// </summary>
        public BackupJob(string name, string sourcePath, string targetPath, BackupType type, BackupManager manager)
        {
            Name = name;
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Type = type;
            Status = new JobStatus(name);

            // Link this job to its status for proper state tracking
            Status.BackupJob = this;

            string appNameToMonitor = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
            _businessMonitor = new BusinessApplicationMonitor(appNameToMonitor);

            // Initialize services
            //_businessMonitor = new BusinessApplicationMonitor(AppSettings.Instance.GetSetting("BusinessSoftwareName") as string);
            _encryptionService = new EncryptionService();
            _backupManager = manager;

            // Select strategy based on backup type
            SetBackupStrategy(type);

            // Attempt to load existing state from state.json
            LoadStateFromPrevious();
        }

        /// <summary>
        /// Loads state information from previous runs if available
        /// </summary>
        private void LoadStateFromPrevious()
        {
            if (Status.Events != null)
            {
                var states = Status.Events.GetAllJobStates();
                var previousState = states.Find(s => s.JobName == Name);

                if (previousState != null &&
                    (previousState.State == BackupState.Paused ||
                     previousState.State == BackupState.Error))
                {
                    // Apply relevant state properties for potential resume
                    Status.TotalFiles = previousState.TotalFiles;
                    Status.TotalSize = previousState.TotalSize;
                    Status.RemainingFiles = previousState.RemainingFiles;
                    Status.RemainingSize = previousState.RemainingSize;

                    // Copy processed files for resume capability
                    if (previousState.ProcessedFiles != null)
                    {
                        foreach (var file in previousState.ProcessedFiles)
                        {
                            Status.AddProcessedFile(file);
                        }
                    }

                    // Update the UI
                    Status.Update();
                }
            }
        }

        /// <summary>
        /// Selects the appropriate backup strategy based on the type
        /// </summary>
        private void SetBackupStrategy(BackupType type)
        {
            _backupFileStrategy = type switch
            {
                BackupType.Full => new FullBackupStrategy(),
                BackupType.Differential => new DiffBackupStrategy(),
                _ => new FullBackupStrategy() // Default to full backup if unknown type
            };
        }

        /// <summary>
        /// Starts the backup job
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _stopRequested = false;

            try
            {
                // Check if business software is running
                if (_businessMonitor.IsRunning())
                {
                    string businessSoftwareName = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
                    string message = $"Business software {businessSoftwareName} detected. Cannot start backup job {Name}.";

                    System.Windows.Forms.MessageBox.Show($"{message}",
                                      "Business Software Detected", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

                    Status.Details = message;
                    Status.SetError(message);
                    _isRunning = false;
                    return;
                }

                // Start the job with start details
                string startDetails = null;
                Status.Start(startDetails);

                // Get files using the selected strategy
                BackupJob jobRef = this;
                toProcessFiles = _backupFileStrategy.GetFiles(ref jobRef);

                // Process files if we have a valid status
                if (Status.State == BackupState.Running)
                {
                    ProcessFiles(toProcessFiles);
                }

                // Complete the job if it wasn't paused or stopped
                if (Status.State == BackupState.Running)
                {
                    Status.Complete();
                }
            }
            catch (Exception ex)
            {
                Status.SetError($"Backup failed: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Processes all files that need to be backed up
        /// </summary>
        private void ProcessFiles(List<String> toProcessFiles)
        {
            // Create target directory if it doesn't exist
            if (!Directory.Exists(TargetPath))
            {
                Directory.CreateDirectory(TargetPath);
            }

            // Process each file that needs to be backed up
            foreach (string sourceFile in toProcessFiles)
            {
                if (_stopRequested || _isPaused)
                    break;

                try
                {
                    Status.CurrentSourceFile = sourceFile;

                    // Build relative and full path to the destination file
                    string relativePath = sourceFile.Substring(SourcePath.Length).TrimStart('\\', '/');
                    string targetFile = Path.Combine(TargetPath, relativePath);
                    Status.CurrentTargetFile = targetFile;
                    _encryptionTime = 0;

                    // Create the target directory if it doesn't exist
                    string? targetDir = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    // Get the size for tracking
                    long fileSize = new FileInfo(sourceFile).Length;

                    // Step 1: copy the source file to the destination
                    File.Copy(sourceFile, targetFile, true);

                    // Step 2: if necessary, encrypt the destination file
                    List<string> encryptionExtensions = new();
                    var extensionsElement = AppSettings.Instance.GetSetting("EncryptionExtensions");
                    if (extensionsElement is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        // Encrypt the file
                        string targetFileRef = targetFile;
                        string encryptionKey = AppSettings.Instance.GetSetting("EncryptionKey") as string;
                        _encryptionTime = _encryptionService.EncryptFile(ref targetFile, encryptionKey);
                    }

                    Status.RemainingFiles--;
                    Status.RemainingSize -= fileSize;
                    Status.EncryptionTimeMs = _encryptionTime;
                    Status.Update();

                    if (_businessMonitor.IsRunning())
                    {
                        string businessSoftwareName = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
                        string message = $"Backup stopped due to business software {businessSoftwareName} being detected while processing file {sourceFile}";

                        System.Windows.Forms.MessageBox.Show($"Business software {businessSoftwareName} detected. " +
                                        $"Backup job {Name} will stop after processing file {sourceFile}.",
                                        "Business Software Detected", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

                        // Add details about the business software stop reason
                        Status.Details = message;
                        Stop();

                        break;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show($"Error processing file {sourceFile}: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    // Continue with next file instead of failing the entire job
                }
            }
        }

        /// <summary>
        /// Pauses the backup job
        /// </summary>
        public void Pause()
        {
            if (_isRunning && !_stopRequested)
            {
                _isPaused = true;
                Status.Pause();
            }
        }

        /// <summary>
        /// Stops the backup job
        /// </summary>
        public void Stop()
        {
            if (_isRunning)
            {
                _stopRequested = true;

                // If Details property is not already set with a specific reason,
                // set a generic reason for stopping
                if (string.IsNullOrEmpty(Status.Details))
                {
                    Status.Details = "Backup stopped by user";
                }

                Status.SetError(Status.Details);
            }
        }

        /// <summary>
        /// Resumes a paused backup job
        /// </summary>
        public void Resume()
        {
            if (_isPaused && !_stopRequested)
            {
                _isPaused = false;
                Status.Resume();

                // Continue processing files
                ProcessFiles(toProcessFiles);

                // Complete the job if we finished successfully
                if (Status.State == BackupState.Running)
                {
                    Status.Complete();
                }

                _isRunning = false;
            }
        }

        /// <summary>
        /// Gets the current status of the job
        /// </summary>
        public JobStatus GetStatus()
        {
            return Status;
        }
    }
}