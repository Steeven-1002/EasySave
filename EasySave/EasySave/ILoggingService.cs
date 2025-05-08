namespace LoggingLibrary
{
    public interface ILoggingService
    {
        void Log(LogEntry entry);
        string GetLogFilePath();
    }
}