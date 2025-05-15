using System;

namespace LoggingLibrary
{
    /// <summary>
    /// Provides logging services for saving log entries to a file.
    /// </summary>
    public class LogService
    {
        private readonly LogFile _logFile;
        private readonly ILogFormatter _logFormatter;
        private readonly string _logDirectoryPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogService"/> class.
        /// </summary>
        /// <param name="logDirectoryPath">The directory path where log files will be stored.</param>
        /// <param name="logFormatter">The formatter used to format log entries.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logFormatter"/> is null.</exception>
        public LogService(string logDirectoryPath, ILogFormatter logFormatter)
        {
            _logDirectoryPath = logDirectoryPath;
            _logFile = new LogFile(logDirectoryPath, logFormatter);
            _logFormatter = logFormatter ?? throw new ArgumentNullException(nameof(logFormatter));
        }

        /// <summary>
        /// Logs an entry with the specified details.
        /// </summary>
        /// <param name="timestamp">The timestamp of the log entry.</param>
        /// <param name="saveName">The name of the save operation.</param>
        /// <param name="sourcePath">The source file path in UNC format.</param>
        /// <param name="targetPath">The target file path in UNC format.</param>
        /// <param name="fileSize">The size of the file being logged, in bytes. Optional.</param>
        /// <param name="durationMs">The duration of the file transfer, in milliseconds. Optional.</param>
        public void Log(DateTime timestamp, string saveName, string sourcePath, string targetPath, long? fileSize = null, double? durationMs = null)
        {
            var logEntry = new LogEntry
            {
                Timestamp = timestamp,
                SaveName = saveName,
                SourcePathUNC = sourcePath,
                TargetPathUNC = targetPath,
                FileSize = fileSize,
                FileTransferTimeMs = durationMs
            };

            string formattedLog = _logFormatter.FormatLog(logEntry);
            _logFile.WriteLogEntry(formattedLog);
        }

        /// <summary>
        /// Gets the directory path where log files are stored.
        /// </summary>
        /// <returns>The directory path as a string.</returns>
        public string GetlogDirectoryPath()
        {
            return _logDirectoryPath;
        }
    }
}