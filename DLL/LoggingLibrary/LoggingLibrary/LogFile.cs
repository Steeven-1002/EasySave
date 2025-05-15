using LoggingLibrary;
using System.Text;

public class LogFile
{
    private readonly string _logDirectoryPath;
    private readonly StringBuilder _buffer = new StringBuilder();
    private readonly ILogFormatter _logFormatter;
    private const long MaxLogFileSizeBytes = 100 * 1024; // 1 Mo
    private string _currentLogFilePath;

    public LogFile(string logDirectoryPath, ILogFormatter logFormatter)
    {
        _logDirectoryPath = logDirectoryPath;
        _logFormatter = logFormatter;

        if (!Directory.Exists(_logDirectoryPath))
        {
            Directory.CreateDirectory(_logDirectoryPath);
        }

        _currentLogFilePath = GenerateNewLogFilePath();
    }

    private string GenerateNewLogFilePath()
    {
        string baseName = DateTime.Now.ToString("yyyy-MM-dd");
        string extension = _logFormatter is JsonLogFormatter ? ".json" : ".xml";


        int index = 0;
        string path;
        do
        {
            string suffix = index == 0 ? "" : $"_{index}";
            string fileName = $"{baseName}{suffix}{extension}";
            path = Path.Combine(_logDirectoryPath, fileName);
            index++;
        } while (File.Exists(path) && new FileInfo(path).Length >= MaxLogFileSizeBytes);

        // Initialise the new log file if not already done
        if (!File.Exists(path))
        {
            File.WriteAllText(path, _logFormatter.InitializeLogFile(path), Encoding.UTF8);
        }

        return path;
    }

    private string GetCurrentLogFilePath()
    {
        if (new FileInfo(_currentLogFilePath).Length >= MaxLogFileSizeBytes)
        {
            // Close the current log file
            FinalizeLogFile(_currentLogFilePath);

            // Create a new log file
            _currentLogFilePath = GenerateNewLogFilePath();
        }

        return _currentLogFilePath;
    }


    ~LogFile() => Close();

    public void WriteLogEntry(string logEntry)
    {
        _buffer.AppendLine(logEntry);
        FlushBuffer();
    }

    public void FlushBuffer()
    {
        if (_buffer.Length > 0)
        {
            string logFilePath = GetCurrentLogFilePath();
            Open(logFilePath);
            File.AppendAllText(logFilePath, _buffer.ToString(), Encoding.UTF8);
            _buffer.Clear();
        }
    }

    public void Close()
    {
        FlushBuffer();

        if (!File.Exists(_currentLogFilePath))
            return;

        string content = File.ReadAllText(_currentLogFilePath, Encoding.UTF8);

        if (_logFormatter is JsonLogFormatter)
        {
            // If output is JSON, remove the last comma if it exists
            int lastCommaIndex = content.LastIndexOf(',');
            int lastBraceIndex = content.LastIndexOf('}');

            // If the last comma is before the last brace, remove it
            if (lastCommaIndex > 0 && lastCommaIndex > lastBraceIndex)
            {
                content = content.Remove(lastCommaIndex, 1);
            }

            // Add the closing bracket depending on the content type
            content += _logFormatter.CloseLogFile();
        }
        else
        {
            content += _logFormatter.CloseLogFile();
        }

        File.WriteAllText(_currentLogFilePath, content, Encoding.UTF8);
    }





    private void FinalizeLogFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            string closing = _logFormatter.CloseLogFile();

            if (_logFormatter is JsonLogFormatter)
            {
                if (content.EndsWith(",\r\n"))
                    content = content[..^3];
                else if (content.EndsWith("[\r\n"))
                    content = "[";

                content += closing;
            }
            else if (_logFormatter is XmlLogFormatter)
            {
                content += closing;
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
    }

    public void Open(string logFilePath)
    {
        if (File.Exists(logFilePath))
        {
            string content = File.ReadAllText(logFilePath, Encoding.UTF8);
            if (content.EndsWith("]"))
                File.WriteAllText(logFilePath, content[..^1] + ",\r\n", Encoding.UTF8);
            else if (content.EndsWith("["))
                File.WriteAllText(logFilePath, content + "\r\n", Encoding.UTF8);
        }
        else
        {
            File.WriteAllText(logFilePath, "[\r\n", Encoding.UTF8);
        }
    }
}
