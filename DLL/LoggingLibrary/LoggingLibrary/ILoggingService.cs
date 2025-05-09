using System;

namespace LoggingLibrary
{
    /// <summary>
    /// Defines a logging service for recording file operations and retrieving log file information.
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Logs details of a file operation.
        /// </summary>
        /// <param name="timestamp">The timestamp of the operation.</param>
        /// <param name="saveName">The name under which the file is saved.</param>
        /// <param name="sourcePath">The source path of the file.</param>
        /// <param name="targetPath">The target path of the file.</param>
        /// <param name="fileSize">The size of the file in bytes, if available.</param>
        /// <param name="durationMs">The duration of the operation in milliseconds, if available.</param>
        void Log(DateTime timestamp, string saveName, string sourcePath, string targetPath, long? fileSize = null, double? durationMs = null);

        /// <summary>
        /// Gets the file path of the log file.
        /// </summary>
        /// <returns>The file path of the log file.</returns>
        string GetLogFilePath();

        /// <summary>
        /// Closes the log file and releases any associated resources.
        /// </summary>
        void CloseLogFile();
    }
}