using LoggingLibrary;
using System.Text;

public class LogFile
{
    private readonly string _logDirectoryPath;
    private readonly StringBuilder _buffer = new StringBuilder();
    private readonly ILogFormatter _logFormatter;
    private const long MaxLogFileSizeBytes = 1 * 1024 * 1024; // 1 Mo
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

        // Initialise le fichier s’il est nouveau
        if (!File.Exists(path))
        {
            File.WriteAllText(path, _logFormatter.InitializeLogFile(path), Encoding.UTF8);
        }

        return path;
    }

    private string GetCurrentLogFilePath()
    {
        // Si le fichier est trop gros, on en crée un nouveau
        if (new FileInfo(_currentLogFilePath).Length >= MaxLogFileSizeBytes)
        {
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
        string logFilePath = _currentLogFilePath;

        if (File.Exists(logFilePath))
        {
            string content = File.ReadAllText(logFilePath, Encoding.UTF8);
            if (content.EndsWith(",\r\n"))
                content = content[..^3] + Environment.NewLine + "]";
            else if (content.EndsWith("[\r\n"))
                content = "[]";
            else if (!content.EndsWith("]"))
                content += "]";

            File.WriteAllText(logFilePath, content, Encoding.UTF8);
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
