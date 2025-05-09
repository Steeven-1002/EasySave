using LoggingLibrary;

namespace LoggingLibrary
{
    /// <summary>
    /// Interface defining methods for formatting and managing log files.
    /// </summary>
    public interface ILogFormatter
    {
        /// <summary>
        /// Formats a log entry into a string representation.
        /// </summary>
        /// <param name="logEntry">The log entry to format.</param>
        /// <returns>A formatted string representation of the log entry.</returns>
        string FormatLog(LogEntry logEntry);

        /// <summary>
        /// Initializes a log file at the specified file path.
        /// </summary>
        /// <param name="logFilePath">The file path where the log file will be created or opened.</param>
        /// <returns>A message indicating the result of the initialization process.</returns>
        string InitializeLogFile(string logFilePath);

        /// <summary>
        /// Closes the currently open log file.
        /// </summary>
        /// <returns>A message indicating the result of the file closing process.</returns>
        string CloseLogFile();
    }
}