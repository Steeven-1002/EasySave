using EasySave.Services;
using EasySave_by_ProSoft.Localization;
using EasySave_by_ProSoft.Services;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;


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
        private ConcurrentQueue<string> toProcessFiles = new ConcurrentQueue<string>();





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
        /// Indicates whether this job was paused by the business application monitor
        /// </summary>
        public bool IsPausedByBusinessApp { get; private set; }

        private readonly object statusLock = new object();

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

            string? appNameToMonitor = AppSettings.Instance.GetSetting("BusinessSoftwareName") as string;
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
                BackupType.Complete => new FullBackupStrategy(),

                BackupType.Differential => new DiffBackupStrategy(),
                BackupType.Differentielle => new DiffBackupStrategy(),
                _ => new FullBackupStrategy() // Default to full backup if unknown type
            };
        }

        /// <summary>
        /// Starts the backup job
        /// </summary>
        public void Start()
        {
            if (_businessMonitor != null && _businessMonitor.IsRunning())
            {
                var dialogService = new DialogService();
                dialogService.ShowBusinessSoftware(localization: Resources.PopUpBusinessSoftware);
                // Set the job status to error when business software is running
                Status.SetError("Cannot start job while business software is running");
                return;
            }

            lock (this)
            {
                // Don't start if already running
                if (_isRunning)
                    return;
                    
                // Reset flags to ensure we can restart after stopping
                _isRunning = true;
                _isPaused = false;
                _stopRequested = false;
                
                // If the job was previously completed or stopped, reset the status
                if (Status.State == BackupState.Completed || 
                    Status.State == BackupState.Error || 
                    Status.State == BackupState.Initialise)
                {
                    Status.ResetForRun();
                }
            }

            try
            {
                // Start the job with start details
                Status.Start();

                // Get files using the selected strategy
                if (toProcessFiles.IsEmpty)
                {
                    BackupJob jobRef = this;
                    var files = _backupFileStrategy.GetFiles(ref jobRef);
                    toProcessFiles = new ConcurrentQueue<string>(files);
                }


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
        private async Task ProcessFiles(ConcurrentQueue<string> toProcessFiles)
        {
            if (!Directory.Exists(TargetPath))
                Directory.CreateDirectory(TargetPath);

            double thresholdKBSetting = (double)(AppSettings.Instance.GetSetting("LargeFileSizeThresholdKey") as double?
                                        ?? AppSettings.Instance.GetSetting("DefaultLargeFileSizeThresholdKey"));
            long largeFileSizeThresholdBytes = (long)(thresholdKBSetting * 1024);

            var ignoredNonPriority = new List<string>();

            // Premier passage : traiter d'abord les fichiers prioritaires
            while (toProcessFiles.TryDequeue(out string sourceFile))
            {
                if (_stopRequested) break;
                while (_isPaused && !_stopRequested)
                {
                    await Task.Delay(500);
                }
                if (_stopRequested) break;

                string ext = Path.GetExtension(sourceFile);
                bool isPriority = PriorityExtensionManager.IsPriorityExtension(ext);

                if (!isPriority && _backupManager != null && _backupManager.HasAnyPendingPriorityFiles())
                {
                    ignoredNonPriority.Add(sourceFile);
                    continue;
                }

                await ProcessLargeFileWithSemaphore(sourceFile, largeFileSizeThresholdBytes);
            }

            // Attendre la fin des fichiers prioritaires
            while (_backupManager != null && _backupManager.HasAnyPendingPriorityFiles())
            {
                if (_stopRequested) break;
                await Task.Delay(500);
            }

            // Second passage : traiter les fichiers non prioritaires
            foreach (string sourceFile in ignoredNonPriority)
            {
                if (_stopRequested) break;
                while (_isPaused && !_stopRequested)
                {
                    await Task.Delay(500);
                }
                if (_stopRequested) break;

                await ProcessLargeFileWithSemaphore(sourceFile, largeFileSizeThresholdBytes);
            }

            // Nettoyage : suppression des fichiers/dossiers cibles qui n'existent plus dans la source
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
        /// Processes a single file for backup
        /// </summary>
        private async Task ProcessSingleFile(string sourceFile, long largeFileSizeThresholdBytes)
        {
            try
            {
                // Check if stop was requested before starting file processing
                if (_stopRequested)
                    return;
                    
                Status.CurrentSourceFile = sourceFile;
                string relativePath = sourceFile.Substring(SourcePath.Length).TrimStart('\\', '/');
                string targetFile = Path.Combine(TargetPath, relativePath);
                Status.CurrentTargetFile = targetFile;

                string? targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                // Check again if stop was requested before file operations
                if (_stopRequested)
                    return;

                long fileSize = _fileSystemService.GetSize(sourceFile);
                _fileSystemService.CopyFile(sourceFile, targetFile);

                // Check if stop was requested before encryption
                if (_stopRequested)
                    return;

                List<string> encryptionExtensions = new();
                var extensionsElement = AppSettings.Instance.GetSetting("EncryptionExtensions");
                if (extensionsElement != null && extensionsElement is JsonElement encryptJson)
                {
                    foreach (var extEncrypt in encryptJson.EnumerateArray())
                    {
                        encryptionExtensions.Add(extEncrypt.GetString()!);
                    }
                }
                if (_encryptionService.ShouldEncrypt(sourceFile, encryptionExtensions))
                {
                    string key = AppSettings.Instance.GetSetting("EncryptionKey") as string ?? string.Empty;
                    if (!string.IsNullOrEmpty(key))
                    {
                        _encryptionTime = _encryptionService.EncryptFile(ref targetFile, key);
                    }
                    else
                    {
                        _encryptionTime = 0;
                        System.Windows.Forms.MessageBox.Show("Encryption key is empty. File will not be encrypted.", "Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    _encryptionTime = 0;
                }

                // Only update status if we haven't been stopped
                if (!_stopRequested)
                {
                    Status.RemainingFiles--;
                    Status.RemainingSize -= fileSize;
                    Status.EncryptionTimeMs = _encryptionTime;
                    Status.Update();
                }
            }
            catch (Exception ex)
            {
                // Only show error if we haven't been stopped
                if (!_stopRequested)
                {
                    System.Windows.Forms.MessageBox.Show($"Error processing file {sourceFile}: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
        }

        private async Task ProcessLargeFileWithSemaphore(string sourceFile, long largeFileSizeThresholdBytes)
        {
            // Check if stop was requested before acquiring semaphore
            if (_stopRequested)
                return;
                
            long fileSize = _fileSystemService.GetSize(sourceFile);
            if (fileSize > largeFileSizeThresholdBytes)
            {
                double thresholdKB = largeFileSizeThresholdBytes / 1024.0;

                // Use a timeout when acquiring the semaphore to prevent deadlock when stopping
                bool semaphoreAcquired = await _largeFileTransferSemaphore.WaitAsync(500);
                if (!semaphoreAcquired)
                {
                    // If we couldn't acquire the semaphore and we're stopping, just return
                    if (_stopRequested)
                        return;
                        
                    // Otherwise try again
                    await _largeFileTransferSemaphore.WaitAsync();
                }
                
                try
                {
                    // Check again if stop was requested after acquiring semaphore
                    if (_stopRequested)
                        return;
                        
                    await ProcessSingleFile(sourceFile, largeFileSizeThresholdBytes);
                }
                finally
                {
                    _largeFileTransferSemaphore.Release();
                }
            }
            else
            {
                await ProcessSingleFile(sourceFile, largeFileSizeThresholdBytes);
            }
        }

        /// <summary>
        /// Pauses the backup job
        /// </summary>
        /// <param name="isPausedByBusinessApp">Indicates if the job was paused by business app monitor</param>
        public void Pause(bool isPausedByBusinessApp = false)
        {
            lock (statusLock)
            {
                if (Status.State != BackupState.Running)
                    return;

                _isPaused = true;
                IsPausedByBusinessApp = isPausedByBusinessApp;
                Status.State = BackupState.Paused;
                Status.Details = isPausedByBusinessApp ? "Job paused by business application" : "Job paused by user";
                Status.Update();
            }
        }

        /// <summary>
        /// Resumes the backup job if it was paused
        /// </summary>
        public void Resume()
        {
            lock (statusLock)
            {
                if (Status.State != BackupState.Paused)
                    return;

                _isPaused = false;
                IsPausedByBusinessApp = false;
                Status.State = BackupState.Running;
                Status.Details = "Job resumed";
                Status.Update();
            }
        }

        /// <summary>
        /// Stops the backup job
        /// </summary>
        public void Stop()
        {
            lock (statusLock)
            {
                if (_isPaused)
                    Resume();
                _stopRequested = true;
                IsPausedByBusinessApp = false;
                Status.State = BackupState.Error;
                Status.Details = "Job stopped by user";
                Status.Update();

                // Force immediate termination of any ongoing operations
                try
                {
                    // Clear the list of files to process to prevent restarting with partial data
                    toProcessFiles.Clear();
                    
                    // Reset internal state flags
                    _isRunning = false;
                    _isPaused = false;
                    
                    // Ensure the job status is properly reset for future runs
                    Status.ResetForRun();
                    Status.State = BackupState.Initialise;
                    Status.Details = "Job ready to start";
                    Status.Update();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during job termination: {ex.Message}");
                }

                // Unregister from business application monitor if needed
                if (_backupManager is BackupManager manager)
                {
                    manager.UnregisterJobFromBusinessMonitor(this);
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

        public IEnumerable<string> GetPendingFiles()
        {
            return toProcessFiles;
        }

        /// <summary>
        /// Asynchronously runs the backup job.
        /// </summary>
        public async Task RunAsync()
        {
            // If you have a synchronous Run() method, you can wrap it:
            // await Task.Run(() => Run());

            // Otherwise, implement your async backup logic here.
            // Example placeholder:
            await Task.CompletedTask;
        }
    }
}