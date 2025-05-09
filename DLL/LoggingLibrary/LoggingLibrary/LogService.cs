using System;

namespace LoggingLibrary
{
    public class LogService
    {
        private readonly LogFile _logFile;
        private readonly ILogFormatter _logFormatter;
        private readonly string _logDirectoryPath; // Stockez le chemin du fichier

        public LogService(string logDirectoryPath, ILogFormatter logFormatter)
        {
            _logDirectoryPath = logDirectoryPath; // Initialisez le chemin
            _logFile = new LogFile(logDirectoryPath, logFormatter);
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

        public string GetlogDirectoryPath()
        {
            return _logDirectoryPath; // Retournez le chemin stocké
        }

        public void CloseLogFile()
        {
            _logFile.Close();
        }
    }
}