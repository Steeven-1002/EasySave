using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoggingLibrary
{
    public class JsonLogFormatter : ILogFormatter
    {
        private readonly JsonSerializerOptions _options;

        public JsonLogFormatter()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public string FormatLog(LogEntry logEntry)
        {
            return JsonSerializer.Serialize(logEntry, _options);
        }
    }
}