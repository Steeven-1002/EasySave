using EasySave_by_ProSoft.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.IO;

namespace EasySave_by_ProSoft.Network
{
    /// <summary>
    /// Represents a message exchanged between client and server
    /// </summary>
    [Serializable]
    public class NetworkMessage
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        public Guid MessageId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Type of message for easy identification and routing
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Raw JSON data payload
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Timestamp when the message was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Creates a new message with specified type and data
        /// </summary>
        public NetworkMessage(string type, object data = null)
        {
            Type = type;
            SetData(data);
        }

        /// <summary>
        /// Parameterless constructor for deserialization
        /// </summary>
        public NetworkMessage() { }

        /// <summary>
        /// Sets the data payload by serializing an object to JSON
        /// </summary>
        public void SetData<T>(T data)
        {
            if (data == null)
            {
                Data = null;
                return;
            }

            try
            {
                // Use consistent serialization options
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter() }
                };
                
                Data = JsonSerializer.Serialize(data, options);
                System.Diagnostics.Debug.WriteLine($"Serialized {typeof(T).Name} data successfully, length: {Data?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error serializing message data of type {typeof(T).Name}: {ex.Message}");
                
                // Try with different options as fallback
                try
                {
                    // Simpler fallback options
                    var fallbackOptions = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };
                    
                    Data = JsonSerializer.Serialize(data, fallbackOptions);
                    System.Diagnostics.Debug.WriteLine($"Fallback serialization successful, length: {Data?.Length ?? 0}");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback serialization also failed: {fallbackEx.Message}");
                    Data = null;
                }
            }
        }

        /// <summary>
        /// Gets the data payload by deserializing from JSON to the specified type
        /// </summary>
        public T GetData<T>()
        {
            if (string.IsNullOrEmpty(Data))
                return default;

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                
                // Trim any potential leading/trailing characters that could cause parsing errors
                string jsonData = Data.Trim();
                
                // Ensure the JSON is properly formatted
                if ((jsonData.StartsWith("{") && jsonData.EndsWith("}")) || 
                    (jsonData.StartsWith("[") && jsonData.EndsWith("]")))
                {
                    try
                    {
                        T result = JsonSerializer.Deserialize<T>(jsonData, options);
                        System.Diagnostics.Debug.WriteLine($"Successfully deserialized data to type {typeof(T).Name}");
                        return result;
                    }
                    catch (JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error deserializing message: {ex.Message}");
                        
                        // Try to fix common JSON issues
                        if (typeof(T) == typeof(List<string>) || typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
                        {
                            // If we're expecting a list but got a single item, try wrapping it in an array
                            if (jsonData.StartsWith("\"") && jsonData.EndsWith("\""))
                            {
                                string fixedJson = $"[{jsonData}]";
                                try
                                {
                                    T result = JsonSerializer.Deserialize<T>(fixedJson, options);
                                    System.Diagnostics.Debug.WriteLine($"Successfully deserialized after wrapping in array");
                                    return result;
                                }
                                catch
                                {
                                    // Ignore this attempt and continue with other recovery options
                                }
                            }
                        }
                        
                        // Try with different options as a last resort
                        try
                        {
                            var fallbackOptions = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            
                            T result = JsonSerializer.Deserialize<T>(jsonData, fallbackOptions);
                            System.Diagnostics.Debug.WriteLine($"Successfully deserialized with fallback options");
                            return result;
                        }
                        catch
                        {
                            throw; // Rethrow the original exception
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid JSON format: {jsonData.Substring(0, Math.Min(50, jsonData.Length))}...");
                    return default;
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deserializing to {typeof(T).Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"JSON data (first 100 chars): {Data.Substring(0, Math.Min(100, Data.Length))}");
                return default;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error deserializing data: {ex.Message}");
                return default;
            }
        }

        // Common message types as constants for consistency
        public static class MessageTypes
        {
            public const string JobStatus = "JobStatus";
            public const string JobStatusRequest = "JobStatusRequest";
            public const string StartJob = "StartJob";
            public const string PauseJob = "PauseJob";
            public const string ResumeJob = "ResumeJob";
            public const string StopJob = "StopJob";
            public const string JobList = "JobList";
            public const string Ping = "Ping";
            public const string Pong = "Pong";
            public const string Error = "Error";
            public const string ClientConnected = "ClientConnected";
            public const string ClientDisconnected = "ClientDisconnected";
        }

        /// <summary>
        /// Creates a job status message with current job states
        /// </summary>
        public static NetworkMessage CreateJobStatusMessage(List<JobState> jobStates)
        {
            try
            {
                if (jobStates == null)
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Creating job status message with null job states");
                    jobStates = new List<JobState>();
                }
                
                // Create a minimal representation with only essential properties to reduce message size
                var simplifiedStates = jobStates.Select(js => new
                {
                    js.JobName,
                    js.SourcePath,
                    js.TargetPath,
                    Type = js.Type.ToString(),
                    State = js.StateAsString,
                    Progress = Math.Round(js.ProgressPercentage, 2),
                    CurrentFile = Path.GetFileName(js.CurrentSourceFile ?? string.Empty),
                    TotalFiles = js.TotalFiles,
                    RemainingFiles = js.RemainingFiles,
                    TotalSize = js.TotalSize,
                    RemainingSize = js.RemainingSize,
                    Timestamp = DateTime.Now.ToString("o")
                }).ToList();
                
                System.Diagnostics.Debug.WriteLine($"Creating job status message with {jobStates.Count} job states");
                return new NetworkMessage(MessageTypes.JobStatus, simplifiedStates);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating job status message: {ex.Message}");
                return new NetworkMessage(MessageTypes.Error, "Failed to create job status message");
            }
        }

        /// <summary>
        /// Creates a message to request current job status
        /// </summary>
        public static NetworkMessage CreateJobStatusRequestMessage()
        {
            return new NetworkMessage(MessageTypes.JobStatusRequest);
        }

        /// <summary>
        /// Creates a message to start a specific job
        /// </summary>
        public static NetworkMessage CreateStartJobMessage(List<string> jobNames)
        {
            return new NetworkMessage(MessageTypes.StartJob, jobNames);
        }

        /// <summary>
        /// Creates a message to pause a specific job
        /// </summary>
        public static NetworkMessage CreatePauseJobMessage(List<string> jobNames)
        {
            return new NetworkMessage(MessageTypes.PauseJob, jobNames);
        }

        /// <summary>
        /// Creates a message to resume a specific job
        /// </summary>
        public static NetworkMessage CreateResumeJobMessage(List<string> jobNames)
        {
            return new NetworkMessage(MessageTypes.ResumeJob, jobNames);
        }

        /// <summary>
        /// Creates a message to stop a specific job
        /// </summary>
        public static NetworkMessage CreateStopJobMessage(List<string> jobNames)
        {
            return new NetworkMessage(MessageTypes.StopJob, jobNames);
        }

        /// <summary>
        /// Creates a ping message for connection testing
        /// </summary>
        public static NetworkMessage CreatePingMessage()
        {
            return new NetworkMessage(MessageTypes.Ping);
        }

        /// <summary>
        /// Creates a pong response to a ping message
        /// </summary>
        public static NetworkMessage CreatePongMessage()
        {
            return new NetworkMessage(MessageTypes.Pong);
        }

        /// <summary>
        /// Creates an error message
        /// </summary>
        public static NetworkMessage CreateErrorMessage(string errorMessage)
        {
            return new NetworkMessage(MessageTypes.Error, errorMessage);
        }
    }
}