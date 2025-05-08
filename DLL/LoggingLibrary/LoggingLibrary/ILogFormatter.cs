using LoggingLibrary;

namespace LoggingLibrary
{
    public interface ILogFormatter
    {
        string FormatLog(LogEntry logEntry);
    }
}