using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EasySave_by_ProSoft.Core;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.Network
{
    /// <summary>
    /// Manages TCP socket connections for remote backup job control
    /// </summary>
    public class SocketServer
    {
        private TcpListener listener;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ConcurrentDictionary<string, TcpClient> connectedClients = new ConcurrentDictionary<string, TcpClient>();
        private Task listenerTask;

        public int Port { get; }
        public bool IsRunning { get; private set; }

        // Event for server status changes
        public event EventHandler<string> ServerStatusChanged;
        public event EventHandler<NetworkMessage> MessageReceived;

        private readonly EventManager _eventManager;
        private readonly BackupManager _backupManager;

        /// <summary>
        /// Initializes a new socket server on the specified port
        /// </summary>
        /// <param name="port">Port to listen on (default: 9000)</param>
        public SocketServer(BackupManager backupManager, int port = 9000)
        {
            Port = port;
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _eventManager = EventManager.Instance;
        }

        /// <summary>
        /// Starts the server and begins listening for client connections
        /// </summary>
        public bool Start()
        {
            if (IsRunning)
                return true;

            try
            {
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start();
                IsRunning = true;

                // Start listening for connections in a background task
                listenerTask = Task.Run(ListenForClientsAsync, cancellationTokenSource.Token);

                RaiseServerStatusChanged($"Server started on port {Port}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting server: {ex.Message}");
                IsRunning = false;
                return false;
            }
        }

        /// <summary>
        /// Stops the server and disconnects all clients
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
                return;

            try
            {
                // Signal cancellation to all tasks
                cancellationTokenSource.Cancel();

                // Close all client connections
                foreach (var client in connectedClients.Values)
                {
                    try
                    {
                        client.Close();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error closing client connection: {ex.Message}");
                    }
                }

                connectedClients.Clear();

                // Stop the listener
                listener?.Stop();
                IsRunning = false;

                RaiseServerStatusChanged("Server stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping server: {ex.Message}");
            }
        }

        /// <summary>
        /// Main loop that listens for and accepts client connections
        /// </summary>
        private async Task ListenForClientsAsync()
        {
            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        // Wait for a client connection
                        client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    }
                    catch (Exception) when (cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        // Normal cancellation
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error accepting client: {ex.Message}");
                        continue;
                    }

                    // Get unique identifier for this client
                    string clientId = Guid.NewGuid().ToString();

                    // Add to connected clients
                    if (connectedClients.TryAdd(clientId, client))
                    {
                        RaiseServerStatusChanged($"Client connected: {GetClientInfo(client)}");

                        // Handle this client in a separate task
                        _ = Task.Run(() => HandleClientAsync(clientId, client), cancellationTokenSource.Token);
                    }
                    else
                    {
                        // Failed to add client for some reason
                        Debug.WriteLine($"Failed to add client {clientId}");
                        client.Close();
                    }
                }
            }
            catch (Exception ex) when (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                Debug.WriteLine($"Error in listener loop: {ex.Message}");
                IsRunning = false;
                RaiseServerStatusChanged($"Server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles communication with a specific client
        /// </summary>
        private async Task HandleClientAsync(string clientId, TcpClient client)
        {
            try
            {
                using (client)
                {
                    // Remove client when we finish handling it
                    using var _ = new ClientCleanupScope(this, clientId);
                    
                    NetworkStream stream = client.GetStream();
                    byte[] lengthBuffer = new byte[4]; // Buffer for the message length prefix
                    byte[] messageBuffer = new byte[1024 * 1024]; // Buffer for the message content (1MB)

                    // Send current job list to the client when they first connect
                    await SendJobStatesToClientAsync(client).ConfigureAwait(false);

                    // Continue reading from client until they disconnect or we're cancelled
                    while (!cancellationTokenSource.Token.IsCancellationRequested && client.Connected)
                    {
                        try
                        {
                            // First read the 4-byte length prefix with a timeout
                            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                            readCts.CancelAfter(TimeSpan.FromSeconds(60)); // 60-second timeout for idle connections
                            
                            int bytesRead = 0;
                            int totalBytesRead = 0;
                            
                            try
                            {
                                // Read the length prefix (4 bytes)
                                totalBytesRead = 0;
                                while (totalBytesRead < 4)
                                {
                                    bytesRead = await stream.ReadAsync(lengthBuffer, totalBytesRead, 4 - totalBytesRead, 
                                        readCts.Token).ConfigureAwait(false);
                                        
                                    if (bytesRead == 0)
                                    {
                                        // Connection closed by client - only exit if confirmed disconnected
                                        if (!IsClientConnected(client))
                                            return;
                                        
                                        // Otherwise, just continue waiting
                                        break;
                                    }
                                    
                                    totalBytesRead += bytesRead;
                                }
                                
                                // If we didn't read a complete length prefix, continue waiting
                                if (totalBytesRead < 4)
                                    continue;
                                
                                // Convert the 4 bytes to an integer (message length)
                                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                                
                                // Simple validation of message length
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
                                
                                totalBytesRead = 0;
                                while (totalBytesRead < messageLength)
                                {
                                    bytesRead = await stream.ReadAsync(messageBuffer, totalBytesRead, 
                                        messageLength - totalBytesRead, msgReadCts.Token).ConfigureAwait(false);
                                        
                                    if (bytesRead == 0)
                                    {
                                        // Connection closed by client - only exit if confirmed disconnected
                                        if (!IsClientConnected(client))
                                            return;
                                        
                                        // Otherwise, break out of the read loop but don't disconnect
                                        break;
                                    }
                                    
                                    totalBytesRead += bytesRead;
                                }
                                
                                // If we didn't read the entire message, continue waiting for new messages
                                if (totalBytesRead < messageLength)
                                    continue;
                                
                                // Convert bytes to string
                                string messageJson = Encoding.UTF8.GetString(messageBuffer, 0, messageLength);
                                
                                // Process the message - don't let exceptions disconnect the client
                                try
                                {
                                    await ProcessMessageAsync(messageJson, client).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error processing message: {ex.Message}");
                                    // Continue without disconnecting
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // Timeout on read, send periodic update if client is still connected
                                if (IsClientConnected(client))
                                {
                                    await SendJobStatesToClientAsync(client).ConfigureAwait(false);
                                }
                                // Continue without disconnecting
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error handling client message: {ex.Message}");
                            // Continue without disconnecting as long as client is connected
                            if (!IsClientConnected(client))
                            {
                                // Only exit if client is definitely disconnected
                                return;
                            }
                            
                            // Add a small delay to avoid tight loop if there are persistent errors
                            await Task.Delay(1000).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling client {clientId}: {ex.Message}");
            }
            finally
            {
                Debug.WriteLine($"Socket Server: Client disconnected: {clientId}");
                RemoveClient(clientId);
            }
        }

        /// <summary>
        /// Processes a message received from a client
        /// </summary>
        private async Task ProcessMessageAsync(string messageJson, TcpClient client)
        {
            try
            {
                Debug.WriteLine($"Processing message: {messageJson.Substring(0, Math.Min(100, messageJson.Length))}...");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };
                
                var message = JsonSerializer.Deserialize<NetworkMessage>(messageJson, options);
                
                if (message == null)
                {
                    Debug.WriteLine("Deserialized message is null");
                    return;
                }
                
                Debug.WriteLine($"Received message of type: {message.Type}, Data: {(string.IsNullOrEmpty(message.Data) ? "null" : message.Data.Substring(0, Math.Min(50, message.Data.Length)))}...");

                // Raise the message received event
                MessageReceived?.Invoke(this, message);

                // Process based on message type
                switch (message.Type)
                {
                    case NetworkMessage.MessageTypes.JobStatusRequest:
                        await SendJobStatesToClientAsync(client).ConfigureAwait(false);
                        break;
                    
                    case NetworkMessage.MessageTypes.StartJob:
                        var jobNamesStart = message.GetData<List<string>>();
                        if (jobNamesStart == null || jobNamesStart.Count == 0)
                        {
                            Debug.WriteLine("Received StartJob command with null or empty job names");
                            break;
                        }
                        _eventManager.NotifyLaunchJobsRequested(jobNamesStart);
                        break;
                    
                    case NetworkMessage.MessageTypes.PauseJob:
                        var jobNamesPause = message.GetData<List<string>>();
                        if (jobNamesPause == null || jobNamesPause.Count == 0)
                        {
                            Debug.WriteLine("Received PauseJob command with null or empty job names");
                            break;
                        }
                        _eventManager.NotifyPauseJobsRequested(jobNamesPause);
                        break;
                    
                    case NetworkMessage.MessageTypes.ResumeJob:
                        var jobNamesResume = message.GetData<List<string>>();
                        if (jobNamesResume == null || jobNamesResume.Count == 0)
                        {
                            Debug.WriteLine("Received ResumeJob command with null or empty job names");
                            break;
                        }
                        _eventManager.NotifyResumeJobsRequested(jobNamesResume);
                        break;
                    
                    case NetworkMessage.MessageTypes.StopJob:
                        var jobNamesStop = message.GetData<List<string>>();
                        if (jobNamesStop == null || jobNamesStop.Count == 0)
                        {
                            Debug.WriteLine("Received StopJob command with null or empty job names");
                            break;
                        }
                        _eventManager.NotifyStopJobsRequested(jobNamesStop);
                        break;
                    
                    case NetworkMessage.MessageTypes.Ping:
                        await SendMessageToClientAsync(NetworkMessage.CreatePongMessage(), client).ConfigureAwait(false);
                        break;
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Error deserializing message: {ex.Message}");
                Debug.WriteLine($"JSON: {messageJson.Substring(0, Math.Min(200, messageJson.Length))}...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends current job states to a client
        /// </summary>
        private async Task SendJobStatesToClientAsync(TcpClient client)
        {
            if (client == null || !IsClientConnected(client))
                return;
                
            try
            {
                // Get all jobs from the backup manager
                var jobs = _backupManager.GetAllJobs();
                
                if (jobs == null || jobs.Count == 0)
                {
                    Debug.WriteLine("No jobs available to send to client");
                    return;
                }
                
                // Create job status snapshots
                var jobStates = new List<JobState>();
                foreach (var job in jobs)
                {
                    if (job != null && job.Status != null)
                    {
                        var snapshot = job.Status.CreateSnapshot();
                        if (snapshot != null)
                        {
                            // Ensure source and target paths are set properly
                            snapshot.SourcePath = job.SourcePath;
                            snapshot.TargetPath = job.TargetPath;
                            snapshot.Type = job.Type;
                            jobStates.Add(snapshot);
                        }
                    }
                }
                
                Debug.WriteLine($"Sending {jobStates.Count} job states to client");
                
                // Create and send the message
                var message = NetworkMessage.CreateJobStatusMessage(jobStates);
                await SendMessageToClientAsync(message, client).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending job states: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts job status to all connected clients
        /// </summary>
        public async Task BroadcastJobStatusAsync(JobStatus status)
        {
            if (!IsRunning || status?.BackupJob == null)
                return;

            try
            {
                // Get all jobs from the backup manager
                var allJobs = _backupManager.GetAllJobs();
                
                if (allJobs == null || allJobs.Count == 0)
                {
                    Debug.WriteLine("No jobs available to send to client");
                    return;
                }
                
                // Create job status snapshots for all jobs
                var jobStates = new List<JobState>();
                foreach (var job in allJobs)
                {
                    if (job != null && job.Status != null)
                    {
                        var snapshot = job.Status.CreateSnapshot();
                        if (snapshot != null)
                        {
                            // Ensure source and target paths are set properly
                            snapshot.SourcePath = job.SourcePath;
                            snapshot.TargetPath = job.TargetPath;
                            snapshot.Type = job.Type;
                            jobStates.Add(snapshot);
                        }
                    }
                }
                
                // Create a message with all job states
                var message = NetworkMessage.CreateJobStatusMessage(jobStates);

                // Get all connected clients
                var clients = connectedClients.Values.ToList();
                
                if (clients.Count == 0)
                {
                    Debug.WriteLine("No clients connected, skipping job status broadcast");
                    return;
                }
                
                Debug.WriteLine($"Broadcasting status update for {jobStates.Count} jobs to {clients.Count} clients");

                // Broadcast to all clients
                foreach (var client in clients)
                {
                    if (IsClientConnected(client))
                    {
                        try
                        {
                            await SendMessageToClientAsync(message, client).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error sending job status to client: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error broadcasting job status: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a message to a specific client
        /// </summary>
        private async Task SendMessageToClientAsync(NetworkMessage message, TcpClient client)
        {
            if (message == null || client == null || !IsClientConnected(client))
                return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Validate message before serializing
                if (string.IsNullOrEmpty(message.Type))
                {
                    Debug.WriteLine("Warning: Attempting to send message with null or empty Type");
                    message.Type = "Unknown";
                }

                // Serialize message
                string messageJson;
                try
                {
                    messageJson = JsonSerializer.Serialize(message, options);
                    Debug.WriteLine($"Serialized message of type {message.Type}, length: {messageJson.Length}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error serializing message: {ex.Message}");
                    return;
                }

                // Convert the message to bytes
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);

                // Create a buffer that includes the message length prefix (4 bytes for length) + message content
                var lengthPrefix = BitConverter.GetBytes(messageBytes.Length);
                var buffer = new byte[4 + messageBytes.Length];

                // Copy the length prefix and message bytes into the buffer
                Buffer.BlockCopy(lengthPrefix, 0, buffer, 0, 4);
                Buffer.BlockCopy(messageBytes, 0, buffer, 4, messageBytes.Length);

                // Use a timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // 2-minute timeout

                try
                {
                    // Double check client is still connected before sending
                    if (!IsClientConnected(client))
                        return;

                    NetworkStream stream = client.GetStream();

                    // Send in one go but don't close connection
                    await stream.WriteAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                    await stream.FlushAsync(cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error sending message to client: {ex.Message}");
                    // Don't remove client on error - it might still be valid
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendMessageToClientAsync: {ex.Message}");
                // Never disconnect the client due to send errors
            }
        }
        /// <summary>
        /// Checks if a client is still connected
        /// </summary>
        private bool IsClientConnected(TcpClient client)
        {
            if (client == null)
                return false;

            try
            {
                var socket = client.Client;
                
                if (socket == null)
                    return false;
                    
                // Check if socket is connected
                if (!socket.Connected)
                    return false;
                
                // This is how you can determine whether a socket is still connected.
                // Poll returns true if socket is closed, has errors, or has data available
                // Available == 0 means socket is closed or has errors
                bool socketClosed = socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0;
                
                return !socketClosed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking client connection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets information about a client (IP address and port)
        /// </summary>
        private string GetClientInfo(TcpClient client)
        {
            try
            {
                return $"{((IPEndPoint)client.Client.RemoteEndPoint).Address}:{((IPEndPoint)client.Client.RemoteEndPoint).Port}";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Raises the ServerStatusChanged event
        /// </summary>
        private void RaiseServerStatusChanged(string status)
        {
            Debug.WriteLine($"Socket Server: {status}");
            ServerStatusChanged?.Invoke(this, status);
        }

        /// <summary>
        /// Gets the client ID for a TcpClient
        /// </summary>
        private string GetClientId(TcpClient client)
        {
            return connectedClients.FirstOrDefault(x => x.Value == client).Key;
        }
        
        /// <summary>
        /// Removes a client by ID
        /// </summary>
        private void RemoveClient(string clientId)
        {
            if (connectedClients.TryRemove(clientId, out var client))
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing client connection: {ex.Message}");
                }
                
                RaiseServerStatusChanged($"Client disconnected: {clientId}");
            }
        }

        /// <summary>
        /// Helper class to ensure clients are removed from the dictionary when disposed
        /// </summary>
        private class ClientCleanupScope : IDisposable
        {
            private readonly SocketServer server;
            private readonly string clientId;

            public ClientCleanupScope(SocketServer server, string clientId)
            {
                this.server = server;
                this.clientId = clientId;
            }

            public void Dispose()
            {
                if (server.connectedClients.TryRemove(clientId, out var client))
                {
                    try
                    {
                        client.Close();
                    }
                    catch { /* Ignore errors during cleanup */ }
                }
            }
        }
    }
}