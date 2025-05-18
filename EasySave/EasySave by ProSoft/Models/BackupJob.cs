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
        private long _totalEncryptionTime = 0;
        public long TotalEncryptionTime => _totalEncryptionTime; // Add this property for logging
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
            Status = new JobStatus();

            string appNameToMonitor = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
            _businessMonitor = new BusinessApplicationMonitor(appNameToMonitor);

            // Initialize services
            //_businessMonitor = new BusinessApplicationMonitor(AppSettings.Instance.GetSetting("BusinessSoftwareName") as string);
            _encryptionService = new EncryptionService();
            _backupManager = manager;

            // Select strategy based on backup type
            SetBackupStrategy(type);
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
                    Status.SetError($"Cannot start backup: Business software {AppSettings.Instance.GetSetting("BusinessSoftwareName")} is running.");
                    _isRunning = false;
                    return;
                }

                // Start the job
                Status.Start();

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
                    Status.CurrentTargetFIle = targetFile;

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
                        foreach (var ext in jsonElement.EnumerateArray())
                        {
                            if (ext.ValueKind == JsonValueKind.String && ext.GetString() is string strExt)
                                encryptionExtensions.Add(strExt);
                        }
                    }

                    // Check if this destination file should be encrypted
                    if (_encryptionService.ShouldEncrypt(targetFile, encryptionExtensions))
                    {
                        string? encryptionKey = AppSettings.Instance.GetSetting("EncryptionKey") as string;
                        if (string.IsNullOrEmpty(encryptionKey))
                            throw new InvalidOperationException("No encryption key defined in settings.");

                        long encryptionTime = _encryptionService.EncryptFile(ref targetFile, encryptionKey);
                        _totalEncryptionTime += encryptionTime;
                    }

                    Status.RemainingFiles--;
                    Status.RemainingSize -= fileSize;
                    Status.Update();
                    if (_businessMonitor.IsRunning())
                    {
                        Console.WriteLine($"Business software {AppSettings.Instance.GetSetting("BusinessSoftwareName")} detected. " +
                                          $"Backup job {Name} will pause after processing file {sourceFile}.");

                        //Pause();
                        Stop();

                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {sourceFile}: {ex.Message}");
                    // Continue processing other files
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
                Status.SetError("Backup stopped by user");
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