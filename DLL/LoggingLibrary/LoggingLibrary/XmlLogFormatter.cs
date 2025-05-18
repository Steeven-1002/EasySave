using System.Xml;
using System.IO;
using System.Xml.Serialization;

namespace LoggingLibrary
{
    /// <summary>
    /// A log formatter that serializes log entries into XML format.
    /// </summary>
    public class XmlLogFormatter : ILogFormatter
    {
        private static readonly XmlSerializer _serializer = new XmlSerializer(
                typeof(LogEntry),
                new XmlRootAttribute("LogEntry")
            );
        private static readonly XmlSerializerNamespaces _namespaces = new();

        static XmlLogFormatter()
        {
            _namespaces.Add(string.Empty, string.Empty);
        }

        /// <summary>
        /// Formats a log entry into an XML string.
        /// </summary>
        /// <param name="logEntry">The log entry to format.</param>
        /// <returns>An XML string representation of the log entry.</returns>
        public string FormatLog(LogEntry logEntry)
        {
            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true
            });

            _serializer.Serialize(xmlWriter, logEntry, _namespaces);
            return stringWriter.ToString();
        }

        /// <summary>
        /// Adds a new log entry to the existing XML content.
        /// </summary>
        /// <param name="existingContent">The existing XML content.</param>
        /// <param name="newContent">The new log entry to add.</param>
        /// <returns>The updated XML content with the new log entry added.</returns>
        public string MergeLogContent(string existingContent, string newContent)
        {
            if (string.IsNullOrEmpty(existingContent))
            {
                // If the existing content is empty, create a valid XML structure
                return $"<LogEntries>{Environment.NewLine}{newContent}</LogEntries>";
            }

            // Remove the closing </LogEntries> tag from the existing content
            int closingTagIndex = existingContent.LastIndexOf("</LogEntries>");
            string? clearContent = null;
            if (closingTagIndex != -1)
            {
                // Remove the closing tag to append the new content
                clearContent = existingContent.Replace("</LogEntries>", string.Empty).TrimEnd();
            }
            else
            {
                clearContent = existingContent;
            }
            // Append the new content and close the </LogEntries> tag
            return $"{clearContent}{newContent}</LogEntries>";
        }
    }
}
