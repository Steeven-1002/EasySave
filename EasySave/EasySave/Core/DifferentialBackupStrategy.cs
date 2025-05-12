using EasySave.Core.Models;
using EasySave.Interfaces;
using EasySave.Services;

namespace EasySave.Core
{
    /// <summary>
    /// Implements the differential backup strategy, which backs up only files that have changed since the last backup.
    /// </summary>
    public class DifferentialBackupStrategy : IBackupStrategy
    {
        private readonly FileSystemService _fileSystemService;
        private readonly List<IBackupObserver> _observers;
        private readonly List<IStateObserver> _stateObservers;

        /// <summary>
        /// Initializes a new instance of the <see cref="DifferentialBackupStrategy"/> class.
        /// </summary>
        /// <param name="fileSystemService">The file system service used for file operations.</param>
        public DifferentialBackupStrategy(FileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
            _observers = new List<IBackupObserver>();
            _stateObservers = new List<IStateObserver>();
        }

        /// <summary>
        /// Registers a backup observer to receive updates during the backup process.
        /// </summary>
        /// <param name="observer">The observer to register.</param>
        public void RegisterObserver(IBackupObserver observer)
        {
            if (!_observers.Contains(observer)) _observers.Add(observer);
        }

        /// <summary>
        /// Registers a state observer to receive state change notifications during the backup process.
        /// </summary>
        /// <param name="stateObserver">The state observer to register.</param>
        public void RegisterStateObserver(IStateObserver stateObserver)
        {
            if (!_stateObservers.Contains(stateObserver)) _stateObservers.Add(stateObserver);
        }

        /// <summary>
        /// Executes the differential backup for the specified backup job.
        /// </summary>
        /// <param name="job">The backup job to execute.</param>
        public void Execute(BackupJob job)
        {
            Console.WriteLine($"DifferentialBackupStrategy: Executing for job '{job.Name}'");
            job.State = BackupState.ACTIVE;

            var filesToBackup = GetFilesToBackup(job);

            long totalSize = filesToBackup.Sum(file => _fileSystemService.GetSize(file));
            int totalFiles = filesToBackup.Count;
            int filesProcessed = 0;
            long currentProcessedFileSize = 0;
            bool errorOccurred = false;

            NotifyStateObservers(job.Name, BackupState.ACTIVE, totalFiles, totalSize, totalFiles, totalSize, "", "");

            foreach (var sourceFilePath in filesToBackup)
            {
                string relativePath = sourceFilePath.Substring(job.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFilePath = Path.Combine(job.TargetPath, relativePath);
                long currentFileSize = _fileSystemService.GetSize(sourceFilePath);
                currentProcessedFileSize += currentFileSize;

                try
                {
                    string? targetDir = Path.GetDirectoryName(targetFilePath);
                    if (targetDir != null && !_fileSystemService.DirectoryExists(targetDir))
                    {
                        _fileSystemService.CreateDirectory(targetDir);
                        NotifyObservers(job.Name, BackupState.ACTIVE, totalFiles, totalSize, totalFiles - filesProcessed, totalSize - currentProcessedFileSize, sourceFilePath, targetFilePath, 0);
                        NotifyStateObservers(job.Name, BackupState.ACTIVE, totalFiles, totalSize, totalFiles - filesProcessed, totalSize - currentProcessedFileSize, sourceFilePath, targetFilePath);
                    }

                    var startTime = DateTime.Now;
                    _fileSystemService.CopyFile(sourceFilePath, targetFilePath);
                    var endTime = DateTime.Now;

                    filesProcessed++;
                    NotifyObservers(job.Name, BackupState.ACTIVE, totalFiles, totalSize, totalFiles - filesProcessed, totalSize - currentProcessedFileSize, sourceFilePath, targetFilePath, endTime.Subtract(startTime).TotalMilliseconds);
                    NotifyStateObservers(job.Name, BackupState.ACTIVE, totalFiles, totalSize, totalFiles - filesProcessed, totalSize - currentProcessedFileSize, sourceFilePath, targetFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR during differential backup of {sourceFilePath}: {ex.Message}");
                    errorOccurred = true;
                    NotifyObservers(job.Name, BackupState.ERROR, totalFiles, totalSize, totalFiles - filesProcessed, totalSize - currentProcessedFileSize, sourceFilePath, targetFilePath, -1);
                    NotifyStateObservers(job.Name, BackupState.ERROR, totalFiles, totalSize, totalFiles - filesProcessed, totalSize - currentProcessedFileSize, sourceFilePath, targetFilePath);
                }
            }

            job.State = !errorOccurred ? BackupState.COMPLETED : BackupState.ERROR;
            NotifyObservers(job.Name, job.State, totalFiles, totalSize, 0, 0, "", "", 0);
            NotifyStateObservers(job.Name, job.State, totalFiles, totalSize, 0, 0, "", "");
        }

        /// <summary>
        /// Retrieves the list of files to back up based on the differential backup strategy.
        /// </summary>
        /// <param name="job">The backup job containing source and target paths.</param>
        /// <returns>A list of file paths to back up.</returns>
        public List<string> GetFilesToBackup(BackupJob job)
        {
            var filesToBackup = new List<string>();
            var sourceFiles = _fileSystemService.GetFilesInDirectory(job.SourcePath);

            foreach (var sourceFilePath in sourceFiles)
            {
                string relativePath = sourceFilePath.Substring(job.SourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFilePath = Path.Combine(job.TargetPath, relativePath);

                if (!_fileSystemService.FileExists(targetFilePath))
                {
                    filesToBackup.Add(sourceFilePath);
                    continue;
                }

                string sourceHash = _fileSystemService.GetFileHash(sourceFilePath);
                string targetHash = _fileSystemService.GetFileHash(targetFilePath);

                if (sourceHash != targetHash)
                {
                    filesToBackup.Add(sourceFilePath);
                }
            }
            return filesToBackup;
        }

        /// <summary>
        /// Notifies all registered backup observers of a state change or progress update.
        /// </summary>
        /// <param name="jobName">The name of the backup job.</param>
        /// <param name="newState">The new state of the backup job.</param>
        /// <param name="totalFiles">The total number of files to back up.</param>
        /// <param name="totalSize">The total size of files to back up.</param>
        /// <param name="remainingFiles">The number of files remaining to back up.</param>
        /// <param name="remainingSize">The size of files remaining to back up.</param>
        /// <param name="currentSourceFile">The current source file being processed.</param>
        /// <param name="currentTargetFile">The current target file being processed.</param>
        /// <param name="transferDuration">The duration of the file transfer in milliseconds.</param>
        private void NotifyObservers(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile, double transferDuration)
        {
            foreach (var observer in _observers)
            {
                observer.Update(jobName, newState, totalFiles, totalSize, remainingFiles, remainingSize, currentSourceFile, currentTargetFile, transferDuration);
            }
        }

        /// <summary>
        /// Notifies all registered state observers of a state change.
        /// </summary>
        /// <param name="jobName">The name of the backup job.</param>
        /// <param name="newState">The new state of the backup job.</param>
        /// <param name="totalFiles">The total number of files to back up.</param>
        /// <param name="totalSize">The total size of files to back up.</param>
        /// <param name="remainingFiles">The number of files remaining to back up.</param>
        /// <param name="remainingSize">The size of files remaining to back up.</param>
        /// <param name="currentSourceFile">The current source file being processed.</param>
        /// <param name="currentTargetFile">The current target file being processed.</param>
        private void NotifyStateObservers(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile)
        {
            foreach (var observer in _stateObservers)
            {
                observer.StateChanged(jobName, newState, totalFiles, totalSize, remainingFiles, remainingSize, currentSourceFile, currentTargetFile);
            }
        }
    }
}