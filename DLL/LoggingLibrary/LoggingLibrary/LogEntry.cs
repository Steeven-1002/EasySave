using System;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text.Json;

namespace LoggingLibrary
{
    public class LogEntry
    {
        [JsonPropertyName("Name")]
        public required string SaveName { get; set; }

        [JsonPropertyName("FileSource")]
        public required string SourcePathUNC { get; set; }

        [JsonPropertyName("FileTarget")]
        public required string TargetPathUNC { get; set; }

        [JsonPropertyName("FileSize")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? FileSize { get; set; }

        [JsonPropertyName("FileTransferTime")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? FileTransferTimeMs { get; set; }

        [JsonPropertyName("time")]
        [JsonConverter(typeof(DateTimeFormatConverter))]
        public DateTime Timestamp { get; set; }

        public LogEntry()
        {
        }
    }

    public class DateTimeFormatConverter : JsonConverter<DateTime>
    {
        private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.ParseExact(reader.GetString(), DateTimeFormat, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(DateTimeFormat));
        }
    }
}