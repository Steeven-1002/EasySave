using EasySave.Core.Models;
using EasySave.Interfaces;
using EasySave.Services;

namespace EasySave.Core
{
    /// <summary>
    /// Implements the full backup strategy, which copies all files from the source directory to the target directory.
    /// </summary>
    public class FullBackupStrategy : IBackupStrategy
    {
        private readonly FileSystemService _fileSystemService;
        private readonly List<IBackupObserver> _observers;
        private readonly List<IStateObserver> _stateObservers;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullBackupStrategy"/> class.
        /// </summary>
        /// <param name="fileSystemService">The file system service used for file operations.</param>
        public FullBackupStrategy(FileSystemService fileSystemService)
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
        /// Unregisters a backup observer to stop receiving updates.
        /// </summary>
        /// <param name="observer">The observer to unregister.</param>
        public void UnregisterObserver(IBackupObserver observer)
        {
            if (_observers.Contains(observer)) _observers.Remove(observer);
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
        /// Unregisters a state observer to stop receiving state change notifications.
        /// </summary>
        /// <param name="stateObserver">The state observer to unregister.</param>
        public void UnregisterStateObserver(IStateObserver stateObserver)
        {
            if (_stateObservers.Contains(stateObserver)) _stateObservers.Remove(stateObserver);
        }

        /// <summary>
        /// Executes the full backup process for the specified backup job.
        /// </summary>
        /// <param name="job">The backup job to execute.</param>
        public void Execute(BackupJob job)
        {
            Console.WriteLine($"FullBackupStrategy: Executing for job '{job.Name}'");
            job.State = BackupState.ACTIVE;

            var filesToBackup = GetFilesToBackup(job);
            long totalSize = filesToBackup.Sum(file => _fileSystemService.GetSize(file));
            int filesProcessed = 0;
            long currentProcessedFileSize = 0;
            bool errorOccurred = false;

            _stateObservers.ForEach(stateObserver =>
            {
                stateObserver.StateChanged(
                    job.Name,
                    job.State,
                    filesToBackup.Count,
                    totalSize,
                    filesToBackup.Count - filesProcessed,
                    totalSize - currentProcessedFileSize,
                    string.Empty, // No source file for the initial state
                    string.Empty  // No target file for the initial state
                );
            });

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
                        _observers.ForEach(observer =>
                        {
                            observer.Update(
                                job.Name,
                                BackupState.ACTIVE,
                                filesToBackup.Count,
                                totalSize,
                                filesToBackup.Count - filesProcessed,
                                totalSize - currentProcessedFileSize,
                                sourceFilePath,
                                targetFilePath,
                                0 // No transfer duration for directory creation
                            );
                        });
                    }

                    // Start timer for file transfer duration
                    var startTime = DateTime.Now;
                    _fileSystemService.CopyFile(sourceFilePath, targetFilePath);
                    var endTime = DateTime.Now;

                    _observers.ForEach(observer =>
                    {
                        observer.Update(
                            job.Name,
                            BackupState.ACTIVE,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath,
                            targetFilePath,
                            endTime.Subtract(startTime).TotalMilliseconds
                        );
                    });

                    _stateObservers.ForEach(stateObserver =>
                    {
                        stateObserver.StateChanged(
                            job.Name,
                            job.State,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath,
                            targetFilePath
                        );
                    });

                    filesProcessed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR during full backup of {sourceFilePath}: {ex.Message}");
                    errorOccurred = true;

                    _observers.ForEach(observer =>
                    {
                        observer.Update(
                            job.Name,
                            BackupState.ERROR,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath,
                            targetFilePath,
                            -1 // Indicate error in transfer duration
                        );
                    });

                    _stateObservers.ForEach(stateObserver =>
                    {
                        stateObserver.StateChanged(
                            job.Name,
                            job.State,
                            filesToBackup.Count,
                            totalSize,
                            filesToBackup.Count - filesProcessed,
                            totalSize - currentProcessedFileSize,
                            sourceFilePath, // Pass the current source file
                            targetFilePath  // Pass the current target file
                        );
                    });
                }
            }

            job.State = !errorOccurred ? BackupState.COMPLETED : BackupState.ERROR;

            _stateObservers.ForEach(stateObserver =>
            {
                stateObserver.StateChanged(
                    job.Name,
                    job.State,
                    filesToBackup.Count,
                    totalSize,
                    filesToBackup.Count - filesProcessed,
                    totalSize - currentProcessedFileSize,
                    string.Empty, // No source file for the final state
                    string.Empty  // No target file for the final state
                );
            });
        }

        /// <summary>
        /// Retrieves the list of files to back up for the specified backup job.
        /// </summary>
        /// <param name="job">The backup job.</param>
        /// <returns>A list of file paths to back up.</returns>
        public List<string> GetFilesToBackup(BackupJob job)
        {
            return _fileSystemService.GetFilesInDirectory(job.SourcePath);
        }

        public void RegisterObserver(Func<string, LoggingBackup> instance)
        {
            throw new NotImplementedException();
        }
    }
}