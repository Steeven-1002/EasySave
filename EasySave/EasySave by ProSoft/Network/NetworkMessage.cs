using EasySave_by_ProSoft.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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
                Data = JsonSerializer.Serialize(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error serializing message data: {ex.Message}");
                Data = null;
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
                return JsonSerializer.Deserialize<T>(Data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deserializing message data to {typeof(T).Name}: {ex.Message}");
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
            return new NetworkMessage(MessageTypes.JobStatus, jobStates);
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