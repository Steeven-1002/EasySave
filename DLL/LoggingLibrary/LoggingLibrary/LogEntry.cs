using System;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.Json;

namespace LoggingLibrary
{
    /// <summary>
    /// Represents a log entry containing details about a file operation.
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// Gets or sets the name of the log entry.
        /// </summary>
        [JsonPropertyName("Name")]
        public required string SaveName { get; set; }

        /// <summary>
        /// Gets or sets the source file path in UNC format.
        /// </summary>
        [JsonPropertyName("FileSource")]
        public required string SourcePathUNC { get; set; }

        /// <summary>
        /// Gets or sets the target file path in UNC format.
        /// </summary>
        [JsonPropertyName("FileTarget")]
        public required string TargetPathUNC { get; set; }

        /// <summary>
        /// Gets or sets the size of the file in bytes. Null if not applicable.
        /// </summary>
        [JsonPropertyName("FileSize")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? FileSize { get; set; }

        /// <summary>
        /// Gets or sets the time taken to transfer the file in milliseconds. Null if not applicable.
        /// </summary>
        [JsonPropertyName("FileTransferTime")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? FileTransferTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the log entry.
        /// </summary>
        [JsonPropertyName("time")]
        [JsonConverter(typeof(DateTimeFormatConverter))]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        public LogEntry()
        {
        }
    }

    /// <summary>
    /// A custom JSON converter for serializing and deserializing <see cref="DateTime"/> objects
    /// in the format "dd/MM/yyyy HH:mm:ss".
    /// </summary>
    public class DateTimeFormatConverter : JsonConverter<DateTime>
    {
        private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss";

        /// <summary>
        /// Reads and converts the JSON string to a <see cref="DateTime"/> object.
        /// </summary>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="typeToConvert">The type to convert to.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>The parsed <see cref="DateTime"/> object.</returns>
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.ParseExact(reader.GetString(), DateTimeFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Writes a <see cref="DateTime"/> object as a JSON string in the specified format.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The <see cref="DateTime"/> value to write.</param>
        /// <param name="options">Serialization options.</param>
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(DateTimeFormat));
        }
    }
}