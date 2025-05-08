using System.IO;
using System.Text;
using System;

namespace LoggingLibrary
{
    public class LogFile
    {
        private readonly string _logDirectoryPath;
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly ILogFormatter _logFormatter;

        public LogFile(string logDirectoryPath, ILogFormatter logFormatter)
        {
            _logDirectoryPath = logDirectoryPath;
            _logFormatter = logFormatter;

            if (!Directory.Exists(_logDirectoryPath))
            {
                Directory.CreateDirectory(_logDirectoryPath);
            }
        }

        private string GetLogFilePath()
        {
            string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";
            return Path.Combine(_logDirectoryPath, fileName);
        }

        ~LogFile() // destructor  
        {
            FlushBuffer();
            string logFilePath = GetLogFilePath();
            if (File.Exists(logFilePath) && new FileInfo(logFilePath).Length > 0)
            {
                File.AppendAllText(logFilePath, _logFormatter.CloseLogFile(), Encoding.UTF8);
            }
        }

        public void WriteLogEntry(string logEntry)
        {
            _buffer.AppendLine(logEntry);
            FlushBuffer();
        }

        public void FlushBuffer()
        {
            if (_buffer.Length > 0)
            {
                string logFilePath = GetLogFilePath();
                if (!File.Exists(logFilePath) || new FileInfo(logFilePath).Length == 0)
                {
                    File.AppendAllText(logFilePath, _logFormatter.InitializeLogFile(logFilePath), Encoding.UTF8);
                }

                File.AppendAllText(logFilePath, _buffer.ToString(), Encoding.UTF8);
                _buffer.Clear();
            }
        }

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
    }
}