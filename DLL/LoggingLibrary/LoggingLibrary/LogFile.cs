using System.IO;
using System.Text;
using System;

namespace LoggingLibrary
{
    /// <summary>
    /// Represents a log file that manages writing and formatting log entries.
    /// </summary>
    public class LogFile
    {
        private readonly string _logDirectoryPath;
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly ILogFormatter _logFormatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogFile"/> class.
        /// </summary>
        /// <param name="logDirectoryPath">The directory path where log files will be stored.</param>
        /// <param name="logFormatter">The formatter used to format log entries.</param>
        public LogFile(string logDirectoryPath, ILogFormatter logFormatter)
        {
            _logDirectoryPath = logDirectoryPath;
            _logFormatter = logFormatter;

            if (!Directory.Exists(_logDirectoryPath))
            {
                Directory.CreateDirectory(_logDirectoryPath);
            }
        }

        /// <summary>
        /// Gets the file path of the current log file based on the current date.
        /// </summary>
        /// <returns>The full path of the log file.</returns>
        private string GetLogFilePath()
        {
            string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";
            return Path.Combine(_logDirectoryPath, fileName);
        }

        /// <summary>
        /// Destructor for the <see cref="LogFile"/> class. Ensures the buffer is flushed and the log file is properly closed.
        /// </summary>
        ~LogFile()
        {
            Close();
        }

        /// <summary>
        /// Writes a log entry to the buffer and flushes it to the log file.
        /// </summary>
        /// <param name="logEntry">The log entry to write.</param>
        public void WriteLogEntry(string logEntry)
        {
            _buffer.AppendLine(logEntry);
            FlushBuffer();
        }

        /// <summary>
        /// Flushes the buffer content to the log file. If the log file does not exist, it initializes it.
        /// </summary>
        public void FlushBuffer()
        {
            if (_buffer.Length > 0)
            {
                string logFilePath = GetLogFilePath();
                if (!File.Exists(logFilePath) || new FileInfo(logFilePath).Length == 0)
                {
                    File.AppendAllText(logFilePath, _logFormatter.InitializeLogFile(logFilePath), Encoding.UTF8);
                }
                Open();
                File.AppendAllText(logFilePath, _buffer.ToString(), Encoding.UTF8);
                _buffer.Clear();
            }
        }

        /// <summary>
        /// Closes the log file by ensuring the buffer is flushed and the file is properly formatted.
        /// </summary>
        public void Close()
        {
            FlushBuffer();
            string logFilePath = GetLogFilePath();
            if (File.Exists(logFilePath))
            {
                string content = File.ReadAllText(logFilePath, Encoding.UTF8);
                if (content.EndsWith(",\r\n"))
                {
                    content = content.Substring(0, content.Length - 3);
                    File.WriteAllText(logFilePath, content + Environment.NewLine + "]", Encoding.UTF8);
                }
                else if (content.EndsWith("[\r\n"))
                {
                    File.WriteAllText(logFilePath, "[]", Encoding.UTF8);
                }
                else if (!content.EndsWith("]"))
                {
                    File.AppendAllText(logFilePath, "]", Encoding.UTF8);
                }
            }
        }
        /// <summary>
        /// Opens the log file for writing. Remove end bracket if exists and add comma.
        /// <summary>
        public void Open()
        {
            string logFilePath = GetLogFilePath();
            if (File.Exists(logFilePath))
            {
                string content = File.ReadAllText(logFilePath, Encoding.UTF8);
                if (content.EndsWith("]"))
                {
                    content = content.Substring(0, content.Length - 1);
                    File.WriteAllText(logFilePath, content + ",\r\n", Encoding.UTF8);
                }
                else if (content.EndsWith("["))
                {
                    File.WriteAllText(logFilePath, content + "\r\n", Encoding.UTF8);
                }
            }
            else
            {
                File.WriteAllText(logFilePath, "[\r\n", Encoding.UTF8);
            }
        }
    }
}