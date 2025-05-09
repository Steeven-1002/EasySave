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
        /// <returns>A JSON string representation of the log entry, followed by a comma.</returns>
        public string FormatLog(LogEntry logEntry)
        {
            return JsonSerializer.Serialize(logEntry, _options) + ",";
        }

        /// <summary>
        /// Initializes the log file with the opening of a JSON array.
        /// </summary>
        /// <param name="logFilePath">The path of the log file to initialize.</param>
        /// <returns>A string representing the start of a JSON array.</returns>
        public string InitializeLogFile(string logFilePath)
        {
            return "[" + Environment.NewLine; // Start the JSON array  
        }

        /// <summary>
        /// Closes the log file with the closing of a JSON array.
        /// </summary>
        /// <returns>A string representing the end of a JSON array.</returns>
        public string CloseLogFile()
        {
            return Environment.NewLine + "]"; // End the JSON array  
        }
    }
}