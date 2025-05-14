using System;
using EasySave.Core.Models;
using EasySave.Interfaces;

using LoggingLibrary;

namespace EasySave.Services
{
    /// <summary>
    /// Singleton class responsible for logging backup operations.
    /// Implements the <see cref="IBackupObserver"/> interface to observe backup job updates.
    /// </summary>
    public class LoggingBackup : IBackupObserver
    {
        /// <summary>
        /// Singleton instance of the <see cref="LoggingBackup"/> class.
        /// </summary>
        private static LoggingBackup? _instance;

        /// <summary>
        /// Service used for logging backup operations.
        /// </summary>
        private readonly LogService _logService;
        
        private static string _logState = "XML"; // Valeur par défaut


        /// <summary>
        /// Private constructor to initialize the logging service with a log file path and formatter.
        /// </summary>
        private LoggingBackup()

        {    if (_logState == "XML") { 

                _logService = new LogService(GetLogFilePath(), new XmlLogFormatter());
            }

            else if (_logState == "JSON")
            {
                _logService = new LogService(GetLogFilePath(), new JsonLogFormatter());
            }
            
        }
        public static void RecreateInstance(string newFormat)
        {
            _logState = newFormat;
            _instance = new LoggingBackup(); // Forcer l’appel du constructeur avec le nouveau _logState
        }


        /// <summary>
        /// Gets the singleton instance of the <see cref="LoggingBackup"/> class.
        /// </summary>
        public static LoggingBackup Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LoggingBackup();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Updates the log with the current state of a backup job.
        /// </summary>
        /// <param name="jobName">The name of the backup job.</param>
        /// <param name="newState">The new state of the backup job.</param>
        /// <param name="totalFiles">The total number of files in the backup job.</param>
        /// <param name="totalSize">The total size of files in the backup job, in bytes.</param>
        /// <param name="remainingFiles">The number of files remaining to be processed.</param>
        /// <param name="remainingSize">The size of files remaining to be processed, in bytes.</param>
        /// <param name="currentSourceFile">The current source file being processed.</param>
        /// <param name="currentTargetFile">The current target file being processed.</param>
        /// <param name="transfertDuration">The duration of the file transfer, in seconds.</param>
        public void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile, double transfertDuration)
        {
            var timestamp = DateTime.Now;
            _logService.Log(
                timestamp,
                jobName,
                currentSourceFile,
                currentTargetFile,
                totalSize - remainingSize,
                transfertDuration
            );

            Console.WriteLine($"[LOG] {timestamp}: Job '{jobName}' updated. State: {newState}, Remaining Files: {remainingFiles}, Remaining Size: {remainingSize} bytes.");
        }

        /// <summary>
        /// Gets the default log file path.
        /// </summary>
        /// <returns>The default log file path as a string.</returns>
        private string GetLogFilePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave", "Logs\\");
        }





    }
}