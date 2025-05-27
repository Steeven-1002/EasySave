using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Core;
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
    public class NetworkClient : IDisposable, IEventListener
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
        private bool isRegisteredWithEventManager = false;

        public string ServerHost { get; private set; }
        public int ServerPort { get; private set; }
        public bool IsConnected 
        { 
            get
            {
                if (client == null || stream == null || !isRunning)
                    return false;
                
                try
                {
                    // Check if the socket is still connected
                    if (!client.Connected)
                        return false;
                    
                    // This is a more reliable way to check if a socket is connected
                    // Poll returns true if socket is closed, has errors, or has data available
                    // Available == 0 means socket is closed or has errors
                    bool socketClosed = client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0;
                    return !socketClosed;
                }
                catch
                {
                    return false;
                }
            }
        }
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
            
            // Register with EventManager to receive job status updates
            RegisterWithEventManager();
        }
        
        /// <summary>
        /// Registers this client as a listener with the EventManager
        /// </summary>
        private void RegisterWithEventManager()
        {
            if (!isRegisteredWithEventManager)
            {
                EventManager.Instance.AddListener(this);
                isRegisteredWithEventManager = true;
                Debug.WriteLine("NetworkClient: Registered with EventManager");
            }
        }
        
        /// <summary>
        /// Unregisters this client from the EventManager
        /// </summary>
        private void UnregisterFromEventManager()
        {
            if (isRegisteredWithEventManager)
            {
                EventManager.Instance.RemoveListener(this);
                isRegisteredWithEventManager = false;
                Debug.WriteLine("NetworkClient: Unregistered from EventManager");
            }
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
                {
                    Debug.WriteLine("Already connected to server, skipping connection attempt");
                    return true;
                }

                // Clean up any existing connection
                Disconnect();

                OnConnectionStatusChanged($"Connecting to {ServerHost}:{ServerPort}...");

                try
                {
                    // Create new cancellation token source
                    cancellationTokenSource = new CancellationTokenSource();
                    
                    // Create and connect the client with timeout
                    client = new TcpClient();
                    
                    // Use a timeout for the connection attempt
                    using var connectTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var connectTask = client.ConnectAsync(ServerHost, ServerPort);
                    
                    try
                    {
                        await connectTask.WaitAsync(connectTimeoutCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException($"Connection attempt to {ServerHost}:{ServerPort} timed out after 5 seconds");
                    }
                    
                    if (!client.Connected)
                    {
                        throw new IOException($"Failed to connect to {ServerHost}:{ServerPort}");
                    }
                    
                    // Configure TCP settings for better reliability
                    client.NoDelay = true; // Disable Nagle's algorithm
                    client.ReceiveTimeout = 30000; // 30 seconds
                    client.SendTimeout = 30000; // 30 seconds
                    client.ReceiveBufferSize = 65536; // 64KB
                    client.SendBufferSize = 65536; // 64KB
                    
                    // Get the network stream
                    stream = client.GetStream();
                    isRunning = true;

                    // Start the receive task
                    receiveTask = Task.Run(ReceiveMessagesAsync, cancellationTokenSource.Token);
                    
                    // Start ping timer
                    StartPingTimer();
                    
                    OnConnectionStatusChanged($"Connected to {ServerHost}:{ServerPort}");
                    
                    // Request initial job statuses
                    try
                    {
                        await GetJobStatusesAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to request initial job statuses: {ex.Message}");
                        // Continue anyway as this is not critical
                    }
                    
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
        /// Gets current job statuses from the server - simplified method for both initial and refresh requests
        /// </summary>
        public async Task GetJobStatusesAsync()
        {
            if (!IsConnected)
            {
                Debug.WriteLine("Cannot request job statuses: not connected");
                throw new InvalidOperationException("Not connected to server");
            }

            try
            {
                Debug.WriteLine("Requesting job statuses from server");
                
                // Create a simple request message
                var message = new NetworkMessage
                {
                    MessageId = Guid.NewGuid(),
                    Type = "JobStatusRequest",
                    Timestamp = DateTime.Now
                };
                
                // Serialize the message
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var messageJson = JsonSerializer.Serialize(message, options);
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);
                
                // Add length prefix
                var lengthPrefix = BitConverter.GetBytes(messageBytes.Length);
                var buffer = new byte[4 + messageBytes.Length];
                
                Buffer.BlockCopy(lengthPrefix, 0, buffer, 0, 4);
                Buffer.BlockCopy(messageBytes, 0, buffer, 4, messageBytes.Length);
                
                // Send with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                if (stream == null)
                {
                    throw new InvalidOperationException("Network stream is null");
                }
                
                await stream.WriteAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);
                
                Debug.WriteLine("Job status request sent successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error requesting job statuses: {ex.Message}");
                OnErrorOccurred(ex);
                throw;
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
                {
                    Debug.WriteLine("Reconnection already in progress, skipping this attempt");
                    return; // Another reconnection attempt is already in progress
                }
                
                try
                {
                    // Force disconnect first to ensure clean state
                    Disconnect();
                    
                    // Double check we're actually disconnected
                    if (client != null || stream != null || isRunning)
                    {
                        Debug.WriteLine("Warning: Connection resources still exist after disconnect, forcing cleanup");
                        // Force cleanup
                        try { stream?.Close(); } catch { }
                        try { stream?.Dispose(); } catch { }
                        stream = null;
                        
                        try { client?.Close(); } catch { }
                        try { client?.Dispose(); } catch { }
                        client = null;
                        
                        isRunning = false;
                    }

                    OnConnectionStatusChanged("Connection lost. Attempting to reconnect...");
                    
                    // Wait before reconnecting with exponential backoff
                    for (int attempt = 1; attempt <= 10; attempt++) // Increase max attempts to 10
                    {
                        if (cancellationTokenSource?.IsCancellationRequested == true)
                        {
                            Debug.WriteLine("Reconnection attempts canceled");
                            return;
                        }
                        
                        // Wait with exponential backoff (1s, 2s, 4s, 8s, 16s, etc.)
                        int delayMs = 1000 * (int)Math.Min(Math.Pow(2, attempt - 1), 30); // Cap at 30 seconds
                        Debug.WriteLine($"Reconnection attempt {attempt}/10, waiting {delayMs/1000} seconds...");
                        await Task.Delay(delayMs).ConfigureAwait(false);
                        
                        try
                        {
                            // Create new cancellation token source
                            cancellationTokenSource = new CancellationTokenSource();
                            
                            // Create and connect the client with timeout
                            client = new TcpClient();
                            
                            // Use a timeout for the connection attempt
                            using var connectTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            var connectTask = client.ConnectAsync(ServerHost, ServerPort);
                            
                            try
                            {
                                await connectTask.WaitAsync(connectTimeoutCts.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                Debug.WriteLine("Connection attempt timed out");
                                continue; // Try again
                            }
                            
                            if (!client.Connected)
                            {
                                Debug.WriteLine("Failed to connect to server");
                                continue; // Try again
                            }
                            
                            // Configure TCP settings for better reliability
                            client.NoDelay = true; // Disable Nagle's algorithm
                            client.ReceiveTimeout = 30000; // 30 seconds
                            client.SendTimeout = 30000; // 30 seconds
                            client.ReceiveBufferSize = 65536; // 64KB
                            client.SendBufferSize = 65536; // 64KB
                            
                            // Get the network stream
                            stream = client.GetStream();
                            isRunning = true;

                            // Start the receive task
                            receiveTask = Task.Run(ReceiveMessagesAsync, cancellationTokenSource.Token);
                            
                            // Start ping timer
                            StartPingTimer();
                            
                            OnConnectionStatusChanged($"Reconnected to {ServerHost}:{ServerPort}");
                            
                            // Request initial job statuses
                            try
                            {
                                await GetJobStatusesAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to request initial job statuses: {ex.Message}");
                                // Continue anyway as this is not critical
                            }
                            
                            Debug.WriteLine("Reconnection successful!");
                            return;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error during reconnection attempt {attempt}: {ex.Message}");
                            
                            // Clean up failed connection attempt
                            try { stream?.Close(); } catch { }
                            try { stream?.Dispose(); } catch { }
                            stream = null;
                            
                            try { client?.Close(); } catch { }
                            try { client?.Dispose(); } catch { }
                            client = null;
                            
                            isRunning = false;
                        }
                    }
                    
                    OnConnectionStatusChanged("Failed to reconnect after multiple attempts. Connection lost.");
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
                
                try
                {
                    reconnectSemaphore.Release();
                }
                catch
                {
                    // Ignore if we can't release the semaphore
                }
            }
        }

        /// <summary>
        /// Continuously receives and processes messages from the server
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            byte[] lengthBuffer = new byte[4]; // Buffer for the message length prefix
            byte[] messageBuffer = new byte[1024 * 1024]; // Buffer for the message content (1MB)
            
            while (cancellationTokenSource != null && !cancellationTokenSource.Token.IsCancellationRequested && isRunning)
            {
                try
                {
                    // Check if we're actually connected
                    if (client == null || stream == null || !IsConnected)
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                        continue;
                    }

                    try
                    {
                        if (cancellationTokenSource == null)
                        {
                            break;
                        }
                        
                        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                        readCts.CancelAfter(TimeSpan.FromSeconds(60)); // 60-second timeout for idle connections
                        
                        int bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, readCts.Token).ConfigureAwait(false);
                        
                        // Check if connection was closed
                        if (bytesRead == 0)
                        {
                            // Only break if we're definitely disconnected
                            if (!IsConnected)
                            {
                                Debug.WriteLine("Server closed connection");
                                break;
                            }
                            
                            // Otherwise, just continue waiting
                            continue;
                        }
                        
                        // If we didn't read all 4 bytes, try to read the rest
                        int totalRead = bytesRead;
                        while (totalRead < 4)
                        {
                            bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, 4 - totalRead, readCts.Token).ConfigureAwait(false);
                            
                            if (bytesRead == 0)
                            {
                                // Only break if we're definitely disconnected
                                if (!IsConnected)
                                {
                                    Debug.WriteLine("Server closed connection while reading length prefix");
                                    break;
                                }
                                
                                // Otherwise, just continue waiting
                                break;
                            }
                            
                            totalRead += bytesRead;
                        }
                        
                        // If we didn't read a complete length prefix, continue waiting
                        if (totalRead < 4)
                            continue;
                        
                        // Convert the 4 bytes to an integer (message length)
                        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                        
                        // Sanity check on message length
                        if (messageLength <= 0 || messageLength > 5 * 1024 * 1024) // Max 5MB
                        {
                            Debug.WriteLine($"Invalid message length: {messageLength}");
                            continue;
                        }
                        
                        // Resize buffer if needed
                        if (messageLength > messageBuffer.Length)
                        {
                            messageBuffer = new byte[messageLength];
                        }
                        
                        // Read the message content with a longer timeout
                        using var msgReadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                        msgReadCts.CancelAfter(TimeSpan.FromMinutes(2)); // 2-minute timeout for message body
                        
                        // Read the message content
                        totalRead = 0;
                        while (totalRead < messageLength)
                        {
                            bytesRead = await stream.ReadAsync(messageBuffer, totalRead, 
                                messageLength - totalRead, msgReadCts.Token).ConfigureAwait(false);
                                
                            if (bytesRead == 0)
                            {
                                // Only break if we're definitely disconnected
                                if (!IsConnected)
                                {
                                    Debug.WriteLine("Server closed connection while reading message body");
                                    break;
                                }
                                
                                // Otherwise, break out of the read loop but don't disconnect
                                break;
                            }
                            
                            totalRead += bytesRead;
                        }
                        
                        // If we didn't read the entire message, continue waiting for new messages
                        if (totalRead < messageLength)
                            continue;
                        
                        // Process the message - don't let exceptions disconnect the client
                        try
                        {
                            string messageJson = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
                            await ProcessMessageAsync(messageJson).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing message: {ex.Message}");
                            // Continue without disconnecting
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout reading from server, just continue waiting
                        // Don't disconnect - server might be busy
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in receive loop: {ex.Message}");
                    
                    // Wait a bit before retrying, but don't disconnect
                    await Task.Delay(1000).ConfigureAwait(false);
                    
                    // Only attempt reconnect if we've definitely lost connection
                    if (!IsConnected && AutoReconnect && isRunning)
                    {
                        Debug.WriteLine("Connection lost. Attempting to reconnect...");
                        _ = AttemptReconnectAsync();
                        // Don't break - let the reconnect happen in the background
                    }
                }
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
            if (message == null)
                return;
                
            // If not connected, queue the message for when we reconnect
            if (!IsConnected)
            {
                Debug.WriteLine("Not connected, cannot send message now");
                
                if (AutoReconnect)
                {
                    Debug.WriteLine("Attempting to reconnect before sending message");
                    _ = AttemptReconnectAsync();
                }
                
                return;
            }

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
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);

                // Check if stream is still available
                if (stream == null || !client.Connected)
                {
                    Debug.WriteLine("Stream or client no longer available");
                    return;
                }

                // Create a buffer with length prefix (4 bytes) + message content
                var lengthPrefix = BitConverter.GetBytes(messageBytes.Length);
                var buffer = new byte[4 + messageBytes.Length];
                
                // Copy the length prefix and message bytes into the buffer
                Buffer.BlockCopy(lengthPrefix, 0, buffer, 0, 4);
                Buffer.BlockCopy(messageBytes, 0, buffer, 4, messageBytes.Length);

                // Use cancellation token to avoid hanging indefinitely
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // 2-minute timeout
                
                // Send in one go
                await stream.WriteAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending message: {ex.Message}");
                
                // Don't disconnect - just let the reconnection happen naturally if needed
                if (!IsConnected && AutoReconnect)
                {
                    Debug.WriteLine("Connection appears to be lost, attempting to reconnect");
                    _ = AttemptReconnectAsync();
                }
            }
        }

        /// <summary>
        /// Sends a request for current job statuses
        /// </summary>
        public async Task RequestJobStatusesAsync()
        {
            try
            {
                await GetJobStatusesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error requesting job statuses: {ex.Message}");
                
                // If we're still connected, try to reconnect
                if (AutoReconnect && !IsConnected)
                {
                    _ = AttemptReconnectAsync();
                }
                
                throw;
            }
        }

        /// <summary>
        /// Sends a job control command to the server
        /// </summary>
        private async Task SendJobControlCommandAsync(string commandType, List<string> jobNames)
        {
            if (jobNames == null || jobNames.Count == 0)
            {
                Debug.WriteLine($"Cannot send {commandType} command: no job names provided");
                return;
            }

            if (!IsConnected)
            {
                Debug.WriteLine($"Cannot send {commandType} command: not connected");
                throw new InvalidOperationException("Not connected to server");
            }

            try
            {
                Debug.WriteLine($"Sending {commandType} command for {jobNames.Count} jobs: {string.Join(", ", jobNames)}");
                
                // Create a command message with the correct message type constants
                var messageType = "";
                switch (commandType)
                {
                    case "StartJob":
                        messageType = NetworkMessage.MessageTypes.StartJob;
                        break;
                    case "PauseJob":
                        messageType = NetworkMessage.MessageTypes.PauseJob;
                        break;
                    case "ResumeJob":
                        messageType = NetworkMessage.MessageTypes.ResumeJob;
                        break;
                    case "StopJob":
                        messageType = NetworkMessage.MessageTypes.StopJob;
                        break;
                    default:
                        messageType = commandType;
                        break;
                }
                
                var message = new NetworkMessage
                {
                    MessageId = Guid.NewGuid(),
                    Type = messageType,
                    Timestamp = DateTime.Now
                };
                
                // Set the job names as data
                message.SetData(jobNames);
                
                // Log the message details for debugging
                Debug.WriteLine($"Job control message details: Type={message.Type}, JobNames={string.Join(", ", jobNames)}");
                
                // Serialize the message
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var messageJson = JsonSerializer.Serialize(message, options);
                Debug.WriteLine($"Serialized message: {messageJson.Substring(0, Math.Min(100, messageJson.Length))}...");
                
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);
                
                // Add length prefix
                var lengthPrefix = BitConverter.GetBytes(messageBytes.Length);
                var buffer = new byte[4 + messageBytes.Length];
                
                Buffer.BlockCopy(lengthPrefix, 0, buffer, 0, 4);
                Buffer.BlockCopy(messageBytes, 0, buffer, 4, messageBytes.Length);
                
                // Send with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                if (stream == null)
                {
                    throw new InvalidOperationException("Network stream is null");
                }
                
                await stream.WriteAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);
                
                Debug.WriteLine($"{commandType} command sent successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending {commandType} command: {ex.Message}");
                OnErrorOccurred(ex);
                
                // Check if we need to reconnect
                if (!IsConnected && AutoReconnect)
                {
                    _ = AttemptReconnectAsync();
                }
                
                throw;
            }
        }

        /// <summary>
        /// Sends a request to start specific jobs
        /// </summary>
        public async Task StartJobsAsync(List<string> jobNames)
        {
            await SendJobControlCommandAsync("StartJob", jobNames);
        }

        /// <summary>
        /// Sends a request to pause specific jobs
        /// </summary>
        public async Task PauseJobsAsync(List<string> jobNames)
        {
            await SendJobControlCommandAsync("PauseJob", jobNames);
        }

        /// <summary>
        /// Sends a request to resume specific jobs
        /// </summary>
        public async Task ResumeJobsAsync(List<string> jobNames)
        {
            await SendJobControlCommandAsync("ResumeJob", jobNames);
        }

        /// <summary>
        /// Sends a request to stop specific jobs
        /// </summary>
        public async Task StopJobsAsync(List<string> jobNames)
        {
            await SendJobControlCommandAsync("StopJob", jobNames);
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
            UnregisterFromEventManager();
            reconnectSemaphore.Dispose();
        }

        #region IEventListener Implementation

        /// <summary>
        /// Handles job status change notifications from the EventManager
        /// </summary>
        public void OnJobStatusChanged(JobStatus status)
        {
            if (IsConnected && status?.BackupJob != null)
            {
                Debug.WriteLine($"NetworkClient: Received job status update for {status.BackupJob.Name}, state: {status.State}");
                
                // Create a snapshot and send to clients
                var snapshot = status.CreateSnapshot();
                if (snapshot != null)
                {
                    // Ensure we have source and target paths
                    snapshot.SourcePath = status.BackupJob.SourcePath;
                    snapshot.TargetPath = status.BackupJob.TargetPath;
                    snapshot.Type = status.BackupJob.Type;
                    
                    // Send the update to the server
                    Task.Run(async () => 
                    {
                        try
                        {
                            var message = NetworkMessage.CreateJobStatusMessage(new List<JobState> { snapshot });
                            await SendMessageAsync(message).ConfigureAwait(false);
                            Debug.WriteLine($"NetworkClient: Sent job status update for {status.BackupJob.Name}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"NetworkClient: Error sending job status update: {ex.Message}");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Handles business software state change notifications from the EventManager
        /// </summary>
        public void OnBusinessSoftwareStateChanged(bool isRunning)
        {
            // Not needed for NetworkClient
        }

        /// <summary>
        /// Handles job launch request notifications from the EventManager
        /// </summary>
        public void OnLaunchJobsRequested(List<string> jobNames)
        {
            // This client doesn't need to handle these events
        }

        /// <summary>
        /// Handles job pause request notifications from the EventManager
        /// </summary>
        public void OnPauseJobsRequested(List<string> jobNames)
        {
            // This client doesn't need to handle these events
        }

        /// <summary>
        /// Handles job resume request notifications from the EventManager
        /// </summary>
        public void OnResumeJobsRequested(List<string> jobNames)
        {
            // This client doesn't need to handle these events
        }

        /// <summary>
        /// Handles job stop request notifications from the EventManager
        /// </summary>
        public void OnStopJobsRequested(List<string> jobNames)
        {
            // This client doesn't need to handle these events
        }

        #endregion
    }
}