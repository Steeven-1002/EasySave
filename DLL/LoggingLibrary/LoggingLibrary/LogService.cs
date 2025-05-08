using System;

namespace LoggingLibrary
{
    public class LogService : ILoggingService
    {
        private readonly LogFile _logFile;
        private readonly ILogFormatter _logFormatter;
        private readonly string _logFilePath; // Stockez le chemin du fichier

        public LogService(string logFilePath, ILogFormatter logFormatter)
        {
            _logFilePath = logFilePath; // Initialisez le chemin
            _logFile = new LogFile(logFilePath);
            _logFormatter = logFormatter ?? throw new ArgumentNullException(nameof(logFormatter));
        }

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

        public string GetLogFilePath()
        {
            return _logFilePath; // Retournez le chemin stocké
        }

        public void CloseLogFile()
        {
            _logFile.Close();
        }
    }
}