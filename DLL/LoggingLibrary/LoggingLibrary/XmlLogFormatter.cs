using System.Text;
using System.Xml.Serialization;

namespace LoggingLibrary
{
    /// <summary>
    /// A log formatter that serializes log entries into XML format.
    /// </summary>
    public class XmlLogFormatter : ILogFormatter
    {
        /// <summary>
        /// Formats a log entry into an XML string.
        /// </summary>
        /// <param name="logEntry">The log entry to format.</param>
        /// <returns>An XML string representation of the log entry.</returns>
        public string FormatLog(LogEntry logEntry)
        {
            var serializer = new XmlSerializer(typeof(LogEntry));
            using var stringWriter = new StringWriter();
            serializer.Serialize(stringWriter, logEntry);
            return stringWriter.ToString();
        }

        /// <summary>
        /// Initializes the XML log file.
        /// </summary>
        /// <param name="logFilePath">The path of the log file to initialize.</param>
        /// <returns>A string representing the start of an XML document.</returns>
        public string InitializeLogFile(string logFilePath)
        {
            return "<LogEntries>" + Environment.NewLine;
        }

        /// <summary>
        /// Closes the XML log file.
        /// </summary>
        /// <returns>A string representing the end of an XML document.</returns>
        public string CloseLogFile()
        {
            return Environment.NewLine + "</LogEntries>";
        }
    }
}
