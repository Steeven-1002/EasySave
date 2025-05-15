using LoggingLibrary;
using System.Text;

public class LogFile
{
    private readonly string _logDirectoryPath;
    private readonly StringBuilder _buffer = new StringBuilder();
    private readonly ILogFormatter _logFormatter;
    private const long MaxLogFileSizeBytes = 100 * 1024; // 1 Mo

    public LogFile(string logDirectoryPath, ILogFormatter logFormatter)
    {
        _logDirectoryPath = logDirectoryPath;
        _logFormatter = logFormatter;

        if (!Directory.Exists(_logDirectoryPath))
        {
            Directory.CreateDirectory(_logDirectoryPath);
        }
    }

    private string GetCurrentLogFile()
    {
        string baseName = DateTime.Now.ToString("yyyy-MM-dd");
        string extension = _logFormatter is JsonLogFormatter ? ".json" : ".xml";

        int index = 1;
        string path;
        do
        {
            string suffix = $"_{index}";
            string fileName = $"{baseName}{suffix}{extension}";
            path = Path.Combine(_logDirectoryPath, fileName);
            index++;
        } while (File.Exists(path) && new FileInfo(path).Length >= MaxLogFileSizeBytes);

        return path;
    }

    public void WriteLogEntry(string logEntry)
    {
        _buffer.AppendLine(logEntry);
        FlushBuffer();
    }

    public void FlushBuffer()
    {
        if (_buffer.Length == 0)
            return;

        string logContent = _buffer.ToString();
        string filePath = GetCurrentLogFile();

        // Retry logic to handle file access conflicts
        const int maxRetries = 3;
        const int delayBetweenRetriesMs = 100;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                string existingContent = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
                File.WriteAllText(filePath, _logFormatter.MergeLogContent(existingContent, logContent));

                // Clear the buffer after successful write
                _buffer.Clear();
                return;
            }
            catch (IOException)
            {
                if (attempt == maxRetries - 1)
                    throw; // Re-throw the exception if max retries are reached

                Thread.Sleep(delayBetweenRetriesMs); // Wait before retrying
            }
        }
    }
}
