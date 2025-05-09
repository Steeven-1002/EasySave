namespace LoggingLibrary
{
    /// <summary>
    /// Interface defining the contract for a logging service.
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Logs a specified entry.
        /// </summary>
        /// <param name="entry">The log entry to be recorded.</param>
        void Log(LogEntry entry);

        /// <summary>
        /// Gets the file path of the log file.
        /// </summary>
        /// <returns>The file path of the log file as a string.</returns>
        string GetLogFilePath();
    }
}