using EasySave_by_ProSoft.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace EasySave_by_ProSoft.Network
{
    /// <summary>
    /// Client for connecting to the EasySave socket server for remote control
    /// </summary>
    public class NetworkClient : IDisposable
    {
        private TcpClient client;
        private NetworkStream stream;
        private CancellationTokenSource cancellationTokenSource;
        private Task receiveTask;
        private bool isRunning = false;
        private readonly object connectionLock = new object();
        private readonly SemaphoreSlim reconnectSemaphore = new SemaphoreSlim(1, 1);
        private readonly TimeSpan pingInterval = TimeSpan.FromSeconds(10);
        private System.Threading.Timer pingTimer;

        public string ServerHost { get; private set; }
        public int ServerPort { get; private set; }
        public bool IsConnected => client?.Connected == true && isRunning;
        public bool AutoReconnect { get; set; } = true;

        // Events
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<NetworkMessage> MessageReceived;
        public event EventHandler<List<JobState>> JobStatusesReceived;
        public event EventHandler<Exception> ErrorOccurred;

        /// <summary>
        /// Creates a new network client
        /// </summary>
        /// <param name="host">Server hostname or IP</param>
        /// <param name="port">Server port</param>
        public NetworkClient(string host = "localhost", int port = 9000)
        {
            ServerHost = host;
            ServerPort = port;
        }

        /// <summary>
        /// Connects to the server
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            // Ensure only one connection attempt at a time
            await reconnectSemaphore.WaitAsync();

            try
            {
                // If already connected, return true
                if (IsConnected)
                    return true;

                // Clean up any existing connection
                Disconnect();

                OnConnectionStatusChanged($"Connecting to {ServerHost}:{ServerPort}...");

                try
                {
                    // Create new cancellation token source
                    cancellationTokenSource = new CancellationTokenSource();
                    
                    // Create and connect the client
                    client = new TcpClient();
                    await client.ConnectAsync(ServerHost, ServerPort).ConfigureAwait(false);
                    
                    // Get the network stream
                    stream = client.GetStream();
                    isRunning = true;

                    // Start the receive task
                    receiveTask = Task.Run(ReceiveMessagesAsync, cancellationTokenSource.Token);
                    
                    // Start ping timer
                    StartPingTimer();
                    
                    OnConnectionStatusChanged($"Connected to {ServerHost}:{ServerPort}");
                    
                    // Request initial job statuses
                    await SendMessageAsync(NetworkMessage.CreateJobStatusRequestMessage()).ConfigureAwait(false);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(ex);
                    OnConnectionStatusChanged($"Connection failed: {ex.Message}");
                    
                    // Clean up failed connection attempt
                    Disconnect();
                    return false;
                }
            }
            finally
            {
                reconnectSemaphore.Release();
            }
        }

        /// <summary>
        /// Disconnects from the server and cleans up resources
        /// </summary>
        public void Disconnect()
        {
            lock (connectionLock)
            {
                // Stop the ping timer
                StopPingTimer();
                
                // Signal cancellation to all tasks
                try
                {
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during cancellation: {ex.Message}");
                }

                // Close the network stream
                try
                {
                    stream?.Close();
                    stream?.Dispose();
                    stream = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing network stream: {ex.Message}");
                }

                // Close the client
                try
                {
                    client?.Close();
                    client?.Dispose();
                    client = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing client: {ex.Message}");
                }

                isRunning = false;
                OnConnectionStatusChanged("Disconnected");
            }
        }

        /// <summary>
        /// Starts the ping timer to keep the connection alive
        /// </summary>
        private void StartPingTimer()
        {
            pingTimer?.Dispose();
            pingTimer = new System.Threading.Timer(async _ => await SendPingAsync(), null, pingInterval, pingInterval);
        }

        /// <summary>
        /// Stops the ping timer
        /// </summary>
        private void StopPingTimer()
        {
            pingTimer?.Dispose();
            pingTimer = null;
        }

        /// <summary>
        /// Sends a ping message to the server
        /// </summary>
        private async Task SendPingAsync()
        {
            if (IsConnected)
            {
                try
                {
                    await SendMessageAsync(NetworkMessage.CreatePingMessage()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error sending ping: {ex.Message}");
                    
                    // Connection might be lost, try to reconnect
                    if (AutoReconnect)
                        _ = AttemptReconnectAsync();
                }
            }
        }

        /// <summary>
        /// Attempts to reconnect to the server
        /// </summary>
        private async Task AttemptReconnectAsync()
        {
            if (!AutoReconnect)
                return;

            try
            {
                // Ensure only one reconnection attempt at a time
                if (!await reconnectSemaphore.WaitAsync(0))
                    return; // Another reconnection attempt is already in progress
                
                try
                {
                    if (IsConnected) // Double-check after acquiring semaphore
                        return;

                    OnConnectionStatusChanged("Connection lost. Attempting to reconnect...");
                    
                    // Clean up old connection
                    Disconnect();
                    
                    // Wait before reconnecting with exponential backoff
                    for (int attempt = 1; attempt <= 5; attempt++)
                    {
                        // Wait with exponential backoff (1s, 2s, 4s, 8s, 16s)
                        int delayMs = 1000 * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delayMs).ConfigureAwait(false);
                        
                        // Try to connect
                        bool connected = await ConnectAsync().ConfigureAwait(false);
                        if (connected)
                            return;
                    }
                    
                    OnConnectionStatusChanged("Disconnected");
                }
                finally
                {
                    reconnectSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during reconnect: {ex.Message}");
                OnErrorOccurred(ex);
                reconnectSemaphore.Release();
            }
        }

        /// <summary>
        /// Continuously receives and processes messages from the server
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            byte[] buffer = new byte[16384]; // 16KB buffer
            
            try
            {
                while (isRunning && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (client == null || stream == null)
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                        continue;
                    }

                    int bytesRead;

                    try
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        // Socket closed or other IO error
                        if (AutoReconnect)
                            _ = AttemptReconnectAsync();
                        break;
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred(ex);
                        
                        if (AutoReconnect)
                            _ = AttemptReconnectAsync();
                        
                        break;
                    }

                    // If we read 0 bytes, the connection has been closed
                    if (bytesRead == 0)
                    {
                        if (AutoReconnect)
                            _ = AttemptReconnectAsync();
                        
                        break;
                    }

                    // Process the received message
                    string messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await ProcessMessageAsync(messageJson).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                OnErrorOccurred(ex);
                
                if (AutoReconnect)
                    _ = AttemptReconnectAsync();
            }
        }

        /// <summary>
        /// Processes a message received from the server
        /// </summary>
        private async Task ProcessMessageAsync(string messageJson)
        {
            try
            {
                // Check for empty or invalid JSON
                if (string.IsNullOrWhiteSpace(messageJson))
                {
                    Debug.WriteLine("Received empty message from server");
                    return;
                }

                // Log the first part of the message for debugging
                Debug.WriteLine($"Processing message (first 50 chars): {messageJson.Substring(0, Math.Min(50, messageJson.Length))}...");

                // Sanitize the JSON string to ensure it's properly formatted
                messageJson = SanitizeJsonString(messageJson);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                
                NetworkMessage message;
                try {
                    message = JsonSerializer.Deserialize<NetworkMessage>(messageJson, options);
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"Error deserializing message: {ex.Message}");
                    Debug.WriteLine($"JSON data (first 200 chars): {messageJson.Substring(0, Math.Min(200, messageJson.Length))}");
                    
                    // Try to recover by finding a valid JSON object
                    int firstBrace = messageJson.IndexOf('{');
                    int lastBrace = messageJson.LastIndexOf('}');
                    
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        string extractedJson = messageJson.Substring(firstBrace, lastBrace - firstBrace + 1);
                        Debug.WriteLine($"Attempting to recover with extracted JSON: {extractedJson.Substring(0, Math.Min(50, extractedJson.Length))}...");
                        
                        try
                        {
                            message = JsonSerializer.Deserialize<NetworkMessage>(extractedJson, options);
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"Recovery attempt failed: {innerEx.Message}");
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                
                if (message == null)
                {
                    Debug.WriteLine("Failed to deserialize message (null result)");
                    return;
                }

                // Raise generic message received event
                OnMessageReceived(message);

                // Handle specific message types
                switch (message.Type)
                {
                    case NetworkMessage.MessageTypes.JobStatus:
                        try
                        {
                            // Define a class that matches the simplified job state structure
                            var jobStates = new List<JobState>();
                            
                            // Use a strongly-typed class instead of dynamic
                            var simplifiedStates = message.GetData<List<SimplifiedJobState>>();
                            if (simplifiedStates != null && simplifiedStates.Count > 0)
                            {
                                Debug.WriteLine($"Received {simplifiedStates.Count} job states");
                                
                                foreach (var item in simplifiedStates)
                                {
                                    var jobState = new JobState
                                    {
                                        JobName = item.JobName ?? "Unknown",
                                        SourcePath = item.SourcePath ?? string.Empty,
                                        TargetPath = item.TargetPath ?? string.Empty,
                                        StateAsString = item.State ?? "WAITING",
                                        ProgressPercentage = item.Progress,
                                        CurrentSourceFile = item.CurrentFile ?? string.Empty,
                                        TotalFiles = item.TotalFiles,
                                        RemainingFiles = item.RemainingFiles,
                                        TotalSize = item.TotalSize,
                                        RemainingSize = item.RemainingSize
                                    };
                                    
                                    // Parse backup type from string
                                    if (!string.IsNullOrEmpty(item.Type) && Enum.TryParse<BackupType>(item.Type, out var backupType))
                                    {
                                        jobState.Type = backupType;
                                    }
                                    
                                    // Convert timestamp if available
                                    if (!string.IsNullOrEmpty(item.Timestamp) && DateTime.TryParse(item.Timestamp, out var dt))
                                    {
                                        jobState.Timestamp = dt;
                                    }
                                    else
                                    {
                                        jobState.Timestamp = DateTime.Now;
                                    }
                                    
                                    jobStates.Add(jobState);
                                }
                                
                                OnJobStatusesReceived(jobStates);
                            }
                            else
                            {
                                Debug.WriteLine("Received empty or null job states list");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing job status: {ex.Message}");
                            Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                            OnErrorOccurred(new Exception($"Error processing job status: {ex.Message}", ex));
                        }
                        break;
                        
                    case NetworkMessage.MessageTypes.Pong:
                        // Ping response received, connection healthy
                        break;
                        
                    case NetworkMessage.MessageTypes.Error:
                        var errorMessage = message.GetData<string>();
                        OnConnectionStatusChanged($"Server error: {errorMessage}");
                        break;
                        
                    default:
                        Debug.WriteLine($"Received message of type: {message.Type}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON error processing message: {ex.Message}");
                Debug.WriteLine($"Message content (first 100 chars): {messageJson.Substring(0, Math.Min(100, messageJson.Length))}");
                OnErrorOccurred(new Exception($"Invalid message format: {ex.Message}", ex));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing message: {ex.Message}");
                Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                OnErrorOccurred(ex);
            }
        }

        /// <summary>
        /// Sanitizes a JSON string to ensure it's properly formatted
        /// </summary>
        private string SanitizeJsonString(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;
                
            // Trim any leading or trailing whitespace
            json = json.Trim();
            
            // Check if we have multiple JSON objects concatenated
            if (json.Contains("}{"))
            {
                Debug.WriteLine("Detected multiple concatenated JSON objects, attempting to fix");
                
                // Try to find the first complete JSON object
                int firstBrace = json.IndexOf('{');
                int matchingBrace = FindMatchingClosingBrace(json, firstBrace);
                
                if (matchingBrace > firstBrace)
                {
                    // Extract just the first complete JSON object
                    json = json.Substring(firstBrace, matchingBrace - firstBrace + 1);
                    Debug.WriteLine($"Extracted first JSON object: {json.Substring(0, Math.Min(50, json.Length))}...");
                }
            }
            
            return json;
        }
        
        /// <summary>
        /// Finds the matching closing brace for an opening brace at the specified position
        /// </summary>
        private int FindMatchingClosingBrace(string text, int openBracePosition)
        {
            int count = 0;
            
            for (int i = openBracePosition; i < text.Length; i++)
            {
                if (text[i] == '{')
                    count++;
                else if (text[i] == '}')
                {
                    count--;
                    if (count == 0)
                        return i;
                }
            }
            
            return -1; // No matching brace found
        }

        /// <summary>
        /// Simplified job state class for deserialization
        /// </summary>
        private class SimplifiedJobState
        {
            public string JobName { get; set; }
            public string SourcePath { get; set; }
            public string TargetPath { get; set; }
            public string Type { get; set; }
            public string State { get; set; }
            public double Progress { get; set; }
            public string CurrentFile { get; set; }
            public int TotalFiles { get; set; }
            public int RemainingFiles { get; set; }
            public long TotalSize { get; set; }
            public long RemainingSize { get; set; }
            public string Timestamp { get; set; }
        }

        /// <summary>
        /// Sends a message to the server
        /// </summary>
        public async Task SendMessageAsync(NetworkMessage message)
        {
            if (!IsConnected || message == null)
                return;

            try
            {
                // Use a more robust serialization approach
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var messageJson = JsonSerializer.Serialize(message, options);
                var buffer = Encoding.UTF8.GetBytes(messageJson);
                
                // Check buffer size and log if it's large
                if (buffer.Length > 8192) // 8KB
                {
                    Debug.WriteLine($"Warning: Large message being sent ({buffer.Length} bytes)");
                    
                    // If it's a job status message and too large, try to reduce the size
                    if (message.Type == NetworkMessage.MessageTypes.JobStatus && buffer.Length > 16384) // 16KB
                    {
                        Debug.WriteLine("Job status message too large, attempting to reduce size");
                        // This will cause the message to be recreated with less data in NetworkMessage.CreateJobStatusMessage
                        return;
                    }
                }

                // Check if stream is still available
                if (stream == null || !client.Connected)
                {
                    throw new InvalidOperationException("Connection lost before sending message");
                }

                // Use cancellation token to avoid hanging indefinitely
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                await stream.WriteAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                OnErrorOccurred(new Exception("Send operation timed out"));
                
                // Disconnect and try to reconnect
                Disconnect();
                
                if (AutoReconnect)
                    _ = AttemptReconnectAsync();
            }
            catch (IOException ex)
            {
                // Socket error or connection closed
                OnErrorOccurred(new Exception($"Connection error: {ex.Message}", ex));
                
                // Disconnect and try to reconnect
                Disconnect();
                
                if (AutoReconnect)
                    _ = AttemptReconnectAsync();
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                
                // Disconnect and try to reconnect
                Disconnect();
                
                if (AutoReconnect)
                    _ = AttemptReconnectAsync();
            }
        }

        /// <summary>
        /// Sends a request for current job statuses
        /// </summary>
        public async Task RequestJobStatusesAsync()
        {
            try
            {
                // Use a timeout to prevent hanging indefinitely
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                // Create a task that sends the request
                var sendTask = SendMessageAsync(NetworkMessage.CreateJobStatusRequestMessage());
                
                // Wait for the task to complete with timeout
                await sendTask.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                OnErrorOccurred(new Exception("Request for job statuses timed out"));
                
                // If timed out and we're still connected, try to reconnect
                if (AutoReconnect && client?.Connected == true)
                    _ = AttemptReconnectAsync();
                
                throw;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                
                if (AutoReconnect)
                    _ = AttemptReconnectAsync();
                
                throw;
            }
        }

        /// <summary>
        /// Sends a request to start specific jobs
        /// </summary>
        public async Task StartJobsAsync(List<string> jobNames)
        {
            if (jobNames == null || jobNames.Count == 0)
                return;
                
            await SendMessageAsync(NetworkMessage.CreateStartJobMessage(jobNames)).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a request to pause specific jobs
        /// </summary>
        public async Task PauseJobsAsync(List<string> jobNames)
        {
            if (jobNames == null || jobNames.Count == 0)
                return;
                
            await SendMessageAsync(NetworkMessage.CreatePauseJobMessage(jobNames)).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a request to resume specific jobs
        /// </summary>
        public async Task ResumeJobsAsync(List<string> jobNames)
        {
            if (jobNames == null || jobNames.Count == 0)
                return;
                
            await SendMessageAsync(NetworkMessage.CreateResumeJobMessage(jobNames)).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a request to stop specific jobs
        /// </summary>
        public async Task StopJobsAsync(List<string> jobNames)
        {
            if (jobNames == null || jobNames.Count == 0)
                return;
                
            await SendMessageAsync(NetworkMessage.CreateStopJobMessage(jobNames)).ConfigureAwait(false);
        }

        // Event invokers
        protected virtual void OnConnectionStatusChanged(string status)
        {
            Debug.WriteLine($"NetworkClient: {status}");
            ConnectionStatusChanged?.Invoke(this, status);
        }

        protected virtual void OnMessageReceived(NetworkMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }

        protected virtual void OnJobStatusesReceived(List<JobState> jobStates)
        {
            JobStatusesReceived?.Invoke(this, jobStates);
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            Debug.WriteLine($"NetworkClient error: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex);
        }

        /// <summary>
        /// Disposes the client and releases resources
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            reconnectSemaphore.Dispose();
        }
    }
}