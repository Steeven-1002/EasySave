using EasySave.Services;
using System.IO;
using System.Text.Json;

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
            if (_isRunning || Status.State == BackupState.Completed)
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
                if (toProcessFiles.Count == 0)
                {
                    BackupJob jobRef = this;
                    toProcessFiles = _backupFileStrategy.GetFiles(ref jobRef);
                }


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
        private void ProcessFiles(List<string> toProcessFiles)
        {
            if (!Directory.Exists(TargetPath))
                Directory.CreateDirectory(TargetPath);

            var filesToHandle = toProcessFiles.ToList();
            var ignoredNonPriority = new List<string>();

            // Première passe : traiter les prioritaires et ignorer les non prioritaires si des prioritaires restent
            foreach (string sourceFile in filesToHandle)
            {
                if (_stopRequested || _isPaused)
                    break;

                string ext = Path.GetExtension(sourceFile);
                bool isPriority = PriorityExtensionManager.IsPriorityExtension(ext);

                if (!isPriority && _backupManager != null && _backupManager.HasPendingPriorityFiles())
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"[IGNORÉ] {sourceFile} (non prioritaire)\nDes fichiers prioritaires restent à traiter.",
                        "Ordre de traitement",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                    ignoredNonPriority.Add(sourceFile);
                    continue;
                }
                else if (isPriority)
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"[PRIORITAIRE] {sourceFile} est traité.",
                        "Ordre de traitement",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"[NORMAL] {sourceFile} est traité (aucun prioritaire restant).",
                        "Ordre de traitement",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                }

                ProcessSingleFile(sourceFile);
                this.toProcessFiles.Remove(sourceFile);
            }

            // Deuxième passe : traiter les fichiers non prioritaires ignorés
            foreach (string sourceFile in ignoredNonPriority)
            {
                if (_stopRequested || _isPaused)
                    break;

                // On vérifie à nouveau qu'il n'y a plus de prioritaires
                if (_backupManager != null && _backupManager.HasPendingPriorityFiles())
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"[ATTENTE] {sourceFile} (non prioritaire) attend toujours la fin des prioritaires.",
                        "Ordre de traitement",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information
                    );
                    continue;
                }

                System.Windows.Forms.MessageBox.Show(
                    $"[NORMAL] {sourceFile} est finalement traité (après les prioritaires).",
                    "Ordre de traitement",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information
                );

                ProcessSingleFile(sourceFile);
                this.toProcessFiles.Remove(sourceFile);
            }

            // Nettoyage des dossiers et fichiers cibles qui n'existent plus dans la source
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


        // Facteurise le traitement d'un fichier unique
        private void ProcessSingleFile(string sourceFile)
        {
            try
            {
                Status.CurrentSourceFile = sourceFile;
                string relativePath = sourceFile.Substring(SourcePath.Length).TrimStart('\\', '/');
                string targetFile = Path.Combine(TargetPath, relativePath);
                Status.CurrentTargetFile = targetFile;

                string? targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                long fileSize = _fileSystemService.GetSize(sourceFile);
                _fileSystemService.CopyFile(sourceFile, targetFile);

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

                    Status.Details = message;
                    Stop();
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error processing file {sourceFile}: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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

        public IEnumerable<string> GetPendingFiles()
        {
            return toProcessFiles;
        }

    }
}