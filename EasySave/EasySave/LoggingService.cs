using System;
using EasySave.Interfaces;
using EasySave.Models;
using LoggingLibrary;

namespace EasySave.Services
{
    public class LoggingBackup : IBackupObserver
    {
        private static LoggingBackup? _instance;
        private readonly LogService _logService;

        private LoggingBackup()
        {
            _logService = new LogService(GetLogFilePath(), new JsonLogFormatter());
        }

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

        public void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile)
        {
            var timestamp = DateTime.Now;
            _logService.Log(
                timestamp,
                jobName,
                currentSourceFile,
                currentTargetFile,
                totalSize - remainingSize,
                (int)(totalFiles - remainingFiles)
            );

            Console.WriteLine($"[LOG] {timestamp}: Job '{jobName}' updated. State: {newState}, Remaining Files: {remainingFiles}, Remaining Size: {remainingSize} bytes.");
        }

        private string GetLogFilePath()
        {
            return "log"; // Default log file path
        }
    }
}
