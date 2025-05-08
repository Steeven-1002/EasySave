using System.IO;
using System.Text;
using System;

namespace LoggingLibrary
{
    public class LogFile
    {
        private readonly string _logFilePath;
        private readonly StringBuilder _buffer = new StringBuilder();
        private const int BufferSize = 1024;

        public LogFile(string logFilePath)
        {
            _logFilePath = logFilePath;
            string directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory) and directory != "")
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(_logFilePath) || new FileInfo(_logFilePath).Length == 0)
            {
                File.AppendAllText(_logFilePath, "[" + Environment.NewLine, Encoding.UTF8);
            }
        }

        public void WriteLogEntry(string logEntry)
        {
            _buffer.AppendLine(logEntry + ",");
            if (_buffer.Length > BufferSize)
            {
                FlushBuffer();
            }
        }

        public void FlushBuffer()
        {
            if (_buffer.Length > 0)
            {
                File.AppendAllText(_logFilePath, _buffer.ToString(), Encoding.UTF8);
                _buffer.Clear();
            }
        }

        public void Close()
        {
            FlushBuffer();
            if (File.Exists(_logFilePath))
            {
                string content = File.ReadAllText(_logFilePath, Encoding.UTF8);
                if (content.EndsWith(",\r\n"))
                {
                    content = content.Substring(0, content.Length - 3);
                    File.WriteAllText(_logFilePath, content + Environment.NewLine + "]", Encoding.UTF8);
                }
                else if (content.EndsWith("[\r\n"))
                {
                    File.WriteAllText(_logFilePath, "[]", Encoding.UTF8);
                }
                else if (!content.EndsWith("]"))
                {
                    File.AppendAllText(_logFilePath, "]", Encoding.UTF8);
                }
            }
        }
    }
}