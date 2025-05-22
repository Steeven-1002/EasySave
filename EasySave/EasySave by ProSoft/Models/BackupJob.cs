using EasySave.Services;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

        private FileSystemService _fileSystemService = new FileSystemService();
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

        private readonly SemaphoreSlim _largeFileTransferSemaphore;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                // If the class implements INotifyPropertyChanged, add:
                // OnPropertyChanged(nameof(IsSelected));
            }
        }

        /// <summary>
        /// Initializes a new backup job
        /// </summary>
        public BackupJob(string name, string sourcePath, string targetPath, BackupType type, BackupManager manager, SemaphoreSlim largeFileSemaphore)
        {
            Name = name;
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Type = type;
            Status = new JobStatus(name);

            // Link this job to its status for proper state tracking
            Status.BackupJob = this;

            _largeFileTransferSemaphore = largeFileSemaphore ?? throw new ArgumentNullException(nameof(largeFileSemaphore));

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
            lock (this)
            {
                if (_isRunning)
                    return;

                _isRunning = true;
                _isPaused = false;
                _stopRequested = false;
            }

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
                    ProcessFiles(toProcessFiles).GetAwaiter().GetResult();
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
        private async Task ProcessFiles(List<String> toProcessFiles)
        {
            // Create target directory if it doesn't exist
            if (!Directory.Exists(TargetPath))
            {
                Directory.CreateDirectory(TargetPath);
            }

            double thresholdKBSetting = (double)(AppSettings.Instance.GetSetting("LargeFileSizeThresholdKey") as double?
                                        ?? AppSettings.Instance.GetSetting("DefaultLargeFileSizeThresholdKey"));
            long largeFileSizeThresholdBytes = (long)(thresholdKBSetting * 1024);

            // Process each file that needs to be backed up
            foreach (string sourceFile in toProcessFiles)
            {
                if (_stopRequested)
                    break;
                while (_isPaused && !_stopRequested)
                {
                    await Task.Delay(500); // Wait a bit before checking again
                }
                if (_stopRequested) break;

                bool isLargeFile = false; // Indicator to know if the semaphore has been taken
                try
                {
                    Status.CurrentSourceFile = sourceFile;

                    // Build relative and full path to the destination file
                    string relativePath = sourceFile.Substring(SourcePath.Length).TrimStart('\\', '/');
                    string targetFile = Path.Combine(TargetPath, relativePath);
                    Status.CurrentTargetFile = targetFile;

                    // Create the target directory if it doesn't exist
                    string? targetDir = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    // Get the size for tracking
                    long fileSize = _fileSystemService.GetSize(sourceFile);

                    if (fileSize > largeFileSizeThresholdBytes)
                    {
                        MessageBox.Show($"Fichier volumineux détecté : {Path.GetFileName(sourceFile)}\nTaille : {fileSize / 1024} Ko (Seuil : {thresholdKBSetting} Ko).\nAttente du sémaphore.", "Debug Fichier Volumineux", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        isLargeFile = true;
                        Status.Details = $"En attente pour transférer un fichier volumineux : {Path.GetFileName(sourceFile)}"; // Inform the user
                        Status.Update(); // Update the UI
                        await _largeFileTransferSemaphore.WaitAsync(); // Wait for the semaphore
                        Status.Details = $"Transfert du fichier volumineux en cours : {Path.GetFileName(sourceFile)}";
                        Status.Update();
                    }

                    // copy the source file to the destination
                    _fileSystemService.CopyFile(sourceFile, targetFile);

                    // if necessary, encrypt the destination file
                    List<string> encryptionExtensions = new();
                    var extensionsElement = AppSettings.Instance.GetSetting("EncryptionExtensions");
                    if (extensionsElement != null && extensionsElement is JsonElement jsonElement)
                    {
                        foreach (var ext in jsonElement.EnumerateArray())
                        {
                            encryptionExtensions.Add(ext.GetString()!);
                        }
                    }
                    if (_encryptionService.ShouldEncrypt(sourceFile, encryptionExtensions))
                    {
                        // Encrypt the file
                        string key = AppSettings.Instance.GetSetting("EncryptionKey") as string ?? string.Empty;
                        if (!string.IsNullOrEmpty(key))
                        {
                            // Encrypt the file and update the encryption time
                            _encryptionTime = _encryptionService.EncryptFile(ref targetFile, key);
                        }
                        else
                        {
                            // If no key is provided, just set the encryption time to 0
                            _encryptionTime = 0;
                            System.Windows.Forms.MessageBox.Show("Encryption key is empty. File will not be encrypted.", "Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        // If not encrypting, just set the encryption time to 0
                        _encryptionTime = 0;
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

                finally
                {
                    if (isLargeFile)
                    {
                        _largeFileTransferSemaphore.Release();
                        Status.Details = ""; // Clear large file specific details
                        Status.Update();
                    }
                }
            }

            // Remove directory in target if not anymore in source
            List<string> sourceDirectories = _fileSystemService.GetDirectoriesInDirectory(SourcePath);
            List<string> targetDirectories = _fileSystemService.GetDirectoriesInDirectory(TargetPath);
            foreach (string targetDirectory in targetDirectories)
            {
                string relativePath = targetDirectory.Substring(TargetPath.Length).TrimStart('\\', '/');
                string sourceDirectory = Path.Combine(SourcePath, relativePath);
                if (!sourceDirectories.Contains(sourceDirectory))
                {
                    _fileSystemService.DeleteDirectory(targetDirectory);
                    Status.CurrentSourceFile = "Directory deleted";
                    Status.CurrentTargetFile = targetDirectory;
                    Status.Update();
                }
            }

            // Remove file in target if not anymore in source
            List<string> sourceFiles = _fileSystemService.GetFilesInDirectory(SourcePath);
            List<string> targetFiles = _fileSystemService.GetFilesInDirectory(TargetPath);
            foreach (string targetFile in targetFiles)
            {
                string relativePath = targetFile.Substring(TargetPath.Length).TrimStart('\\', '/');
                string sourceFile = Path.Combine(SourcePath, relativePath);
                if (!sourceFiles.Contains(sourceFile))
                {
                    _fileSystemService.DeleteFile(targetFile);
                    Status.CurrentSourceFile = "File deleted";
                    Status.CurrentTargetFile = targetFile;
                    Status.Update();
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
                _isPaused = false;

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

                // Complete the job if we finished successfully
                if (!_isRunning && Status.State == BackupState.Paused)
                {
                    _isRunning = true;
                }
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