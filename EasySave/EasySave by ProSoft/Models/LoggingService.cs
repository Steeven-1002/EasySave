using LoggingLibrary;
using System.IO;

namespace EasySave_by_ProSoft.Models
{
    public class LoggingService : JobEventListeners
    {
        /// <summary>
        /// Singleton instance of the <see cref="LoggingService"/> class.
        /// </summary>
        private static LoggingService? _instance;

        /// <summary>
        /// Service used for logging backup operations.
        /// </summary>
        private LogService? _logService;
        private static string _logFormat = AppSettings.Instance.GetSetting("LogFormat")?.ToString() ?? "XML";

        /// <summary>
        /// Private constructor to initialize the logging service with a log file path and formatter.
        /// </summary>
        private LoggingService()
        {
            _logService = _logFormat switch
            {
                "XML" => new LogService(GetLogFilePath(), new XmlLogFormatter()),
                "JSON" => new LogService(GetLogFilePath(), new JsonLogFormatter()),
                _ => throw new InvalidOperationException("Invalid log format specified.")
            };
        }

        /// <summary>
        /// Recreates the singleton instance with a new log format.
        /// </summary>
        /// <param name="newFormat">The new log format ("XML" or "JSON").</param>
        public static void RecreateInstance(string newFormat)
        {
            _logFormat = newFormat;
            AppSettings.Instance.SetSetting("LogFormat", newFormat);
            AppSettings.Instance.SaveConfiguration();

            // Ensure _instance is not null before accessing its members
            if (_instance != null)
            {
                _instance._logService = null; // Dispose or release old service if necessary
                _instance._logService = newFormat switch
                {
                    "XML" => new LogService(_instance.GetLogFilePath(), new XmlLogFormatter()),
                    "JSON" => new LogService(_instance.GetLogFilePath(), new JsonLogFormatter()),
                    _ => throw new InvalidOperationException("Invalid log format specified.")
                };
            }
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="LoggingService"/> class.
        /// </summary>
        public static LoggingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LoggingService();
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
        public void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile, double transfertDuration, double encryptionTimeMs, string details = null)
        {
            var timestamp = DateTime.Now;
            _logService.Log(
                timestamp,
                jobName,
                currentSourceFile,
                currentTargetFile,
                totalSize - remainingSize,
                transfertDuration,
                encryptionTimeMs,
                details
            );
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
