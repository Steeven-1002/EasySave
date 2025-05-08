using System;

namespace LoggingLibrary
{
    public interface ILoggingService
    {
        void Log(DateTime timestamp, string saveName, string sourcePath, string targetPath, long? fileSize = null, double? durationMs = null);
        string GetLogFilePath();

        void CloseLogFile();
    }
}