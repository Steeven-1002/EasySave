using LoggingLibrary;

namespace LoggingLibrary
{
    public interface ILogFormatter
    {
        string FormatLog(LogEntry logEntry);

        // File initialization method
        string InitializeLogFile(string logFilePath);
        // File closing method
        string CloseLogFile();
    }
}