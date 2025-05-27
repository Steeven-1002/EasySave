# Documentation of the `LoggingLibrary`

This library provides logging capabilities for file operation details. It supports different log formatting options (JSON and XML).

## Interfaces

### `ILogFormatter.cs`
* **Purpose**: Defines methods for formatting log entries and merging new log content into existing log content.
* **Methods**:
    * `FormatLog(LogEntry logEntry)`: Formats a `LogEntry` object into a textual representation.
    * `MergeLogContent(string existingContent, string newContent)`: Merges new log content with existing log content.

### `ILoggingService.cs`
* **Purpose**: Defines a logging service for recording file operations and retrieving log file information.
* **Methods**:
    * `Log(DateTime timestamp, string saveName, string sourcePath, string targetPath, long? fileSize = null, double? durationMs = null, string? details = null)`: Logs details of a file operation.
    * `GetLogFilePath()`: Gets the path of the log file.
    * `CloseLogFile()`: Closes the log file and releases associated resources.

## Classes

### `JsonLogFormatter.cs`
* **Purpose**: An implementation of `ILogFormatter` that serializes log entries in JSON format.
* **Constructor**:
    * `JsonLogFormatter()`: Initializes a new instance with default JSON serialization options (indented writing, null values ignored).
* **Methods**:
    * `FormatLog(LogEntry logEntry)`: Serializes a `LogEntry` into a JSON string.
    * `MergeLogContent(string existingContent, string newContent)`: Adds a new log entry to the existing JSON content, handling the creation of the initial array and appending entries.

### `LogEntry.cs`
* **Purpose**: Represents a single log entry containing details of a file operation.
* **Properties**:
    * `SaveName` (string): The name under which the file is saved.
    * `SourcePathUNC` (string): UNC path of the source file.
    * `TargetPathUNC` (string): UNC path of the target file.
    * `FileSize` (long?, optional): File size in bytes.
    * `FileTransferTimeMs` (double?, optional): Duration of the file transfer in milliseconds.
    * `Timestamp` (DateTime): Timestamp of the operation.
    * `EncryptionTimsMs` (double?, optional): Duration of the encryption process in milliseconds.
    * `Details` (string?, optional): Additional details about the operation.
* **Class `DateTimeFormatConverter`**: A custom JSON converter for `DateTime` objects, ensuring serialization and deserialization in the "dd/MM/yyyy HH:mm:ss" format.

### `LogFile.cs`
* **Purpose**: Manages writing log entries to files, including file rotation based on size and daily naming.
* **Constructor**:
    * `LogFile(string logDirectoryPath, ILogFormatter logFormatter)`: Initializes a new instance, creating the log directory if it doesn't exist.
* **Methods**:
    * `WriteLogEntry(string logEntry)`: Adds a formatted log entry to an internal buffer and flushes it to the file.
    * `FlushBuffer()`: Writes the buffered log content to the current log file, handling file access conflicts with retry logic.
* **Internal Logic**:
    * Log files are named by date (e.g., "YYYY-MM-DD_X.json" or "YYYY-MM-DD_X.xml").
    * New log files are created if the current file exceeds `MaxLogFileSizeBytes` (100 KB).

### `LogService.cs`
* **Purpose**: Provides the main logging functionality, using `LogFile` and `ILogFormatter` to record log entries.
* **Constructor**:
    * `LogService(string logDirectoryPath, ILogFormatter logFormatter)`: Initializes the service with the specified log directory and formatter.
* **Methods**:
    * `Log(DateTime timestamp, string saveName, string sourcePath, string targetPath, long? fileSize = null, double? durationMs = null, double? encryptionTimeMs = null, string? details = null)`: Creates a `LogEntry` from the given parameters, formats it using the injected formatter, and writes it to the log file.
    * `GetlogDirectoryPath()`: Returns the path of the directory where log files are stored.

### `XmlLogFormatter.cs`
* **Purpose**: An implementation of `ILogFormatter` that serializes log entries in XML format.
* **Methods**:
    * `FormatLog(LogEntry logEntry)`: Serializes a `LogEntry` into an XML string, omitting the XML declaration and indenting the output.
    * `MergeLogContent(string existingContent, string newContent)`: Merges a new log entry into the existing XML content, ensuring the output remains a valid XML structure with a `<LogEntries>` root element.
