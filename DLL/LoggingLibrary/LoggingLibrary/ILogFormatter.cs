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
        /// Merges a new log entry into the existing log content.
        /// </summary>
        /// <param name="existingContent">The existing log content.</param>
        /// <param name="newContent">The new log entry to add.</param>
        /// <returns>The updated log content with the new entry added.</returns>
        string MergeLogContent(string existingContent, string newContent);
    }
}