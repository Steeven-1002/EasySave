using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoggingLibrary
{
    /// <summary>
    /// A log formatter that serializes log entries into JSON format.
    /// </summary>
    public class JsonLogFormatter : ILogFormatter
    {
        private readonly JsonSerializerOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonLogFormatter"/> class with default JSON serialization options.
        /// </summary>
        public JsonLogFormatter()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Formats a log entry into a JSON string.
        /// </summary>
        /// <param name="logEntry">The log entry to format.</param>
        /// <returns>A JSON string representation of the log entry.</returns>
        public string FormatLog(LogEntry logEntry)
        {
            // Serialize the log entry into JSON format
            return JsonSerializer.Serialize(logEntry, _options);
        }

        /// <summary>
        /// Adds a new log entry to the existing JSON content.
        /// </summary>
        /// <param name="existingContent">The existing JSON content.</param>
        /// <param name="newContent">The new log entry to add.</param>
        /// <returns>The updated JSON content with the new log entry added.</returns>
        public string MergeLogContent(string existingContent, string newContent)
        {
            if (string.IsNullOrEmpty(existingContent))
            {
                // If the existing content is empty, create a valid JSON array
                return $"[{Environment.NewLine}{newContent}]";
            }

            // Remove the closing bracket of the JSON array
            existingContent = existingContent.Replace(Environment.NewLine + "]", string.Empty).TrimEnd(',');

            // Append the new content and close the JSON array
            return $"{existingContent},{Environment.NewLine}{newContent}]";
        }
    }
}