using System;
using System.IO;
using LoggingLibrary;

namespace EasySave_by_ProSoft.Models 
{
	public class LoggingService : JobEventListeners 
	{
		private static LoggingService? _instance;
		private LoggingBackup? _loggingBackupInstance;
		private string _logState;
		private readonly LoggingLibrary.LoggingLibrary _loggingLibrary;
        
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

        private LoggingService() 
		{
			// Initialize the logging library
			_loggingLibrary = new LoggingLibrary.LoggingLibrary();
			
			// Create the log directory if it doesn't exist
			string logDirectory = GetLogDirectoryPath();
			if (!Directory.Exists(logDirectory))
			{
				Directory.CreateDirectory(logDirectory);
			}
			
			// Initialize with the current format from settings
			_logState = Settings.Instance.LogFormat.ToString();
			_loggingBackupInstance = new LoggingBackup(_logState);
        }
		
		public void RecreateInstance(ref string newFormat) 
		{
			if (_logState != newFormat)
			{
				_logState = newFormat;
				_loggingBackupInstance = new LoggingBackup(newFormat);
			}
		}
		
		public void Update(ref JobStatus jobStatus) 
		{
			if (_loggingBackupInstance != null)
			{
				// Create log entry with data from JobStatus
				LogEntry logEntry = new LogEntry
				{
					Timestamp = DateTime.Now,
					JobName = "BackupJob", // Should be replaced with actual job name
					SourceFile = jobStatus.CurrentSourceFile ?? string.Empty,
					TargetFile = jobStatus.CurrentTargetFIle ?? string.Empty,
					FileSize = jobStatus.TotalSize,
					// Add other required fields
					TransferTime = 0, // Should be calculated based on actual transfer time
					CryptTime = 0 // Should be populated with encryption time if applicable
				};
				
				// Write the log entry
				_loggingBackupInstance.WriteLog(logEntry);
			}
		}
		
		private string GetLogFilepath() 
		{
			string logDirectory = GetLogDirectoryPath();
			string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
			string extension = Settings.Instance.LogFormat == LogFormat.JSON ? ".json" : ".xml";
			
			return Path.Combine(logDirectory, $"{dateStr}{extension}");
		}
		
		private string GetLogDirectoryPath()
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"EasySave",
				"Logs"
			);
		}
		
		public JobStatus Update(ref object jobStatus) 
		{
			if (jobStatus is JobStatus status)
			{
				Update(ref status);
				return status;
			}
			return new JobStatus();
		}
    }
}
