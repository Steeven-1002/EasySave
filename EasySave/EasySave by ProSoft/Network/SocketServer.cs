using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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
                    byte[] buffer = new byte[16384]; // 16KB buffer
                    int bytesRead;

                    // Send current job list to the client when they first connect
                    await SendJobStatesToClientAsync(client).ConfigureAwait(false);

                    // Continue reading from client until they disconnect or we're cancelled
                    while (!cancellationTokenSource.Token.IsCancellationRequested && client.Connected)
                    {
                        // Use ReadAsync with a cancellation token
                        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                        readCts.CancelAfter(TimeSpan.FromSeconds(1)); // 1-second timeout

                        try
                        {
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, readCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            // Normal cancellation
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            // Timeout on read, check connection and continue
                            if (!IsClientConnected(client))
                                break;
                            continue;
                        }
                        catch (IOException)
                        {
                            // Socket closed or connection error
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error reading from client {clientId}: {ex.Message}");
                            break;
                        }

                        // If we read 0 bytes, client has disconnected
                        if (bytesRead == 0)
                            break;

                        // Process the message
                        string messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        await ProcessMessageAsync(messageJson, client).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling client {clientId}: {ex.Message}");
            }
            finally
            {
                RaiseServerStatusChanged($"Client disconnected: {clientId}");
            }
        }

        /// <summary>
        /// Processes a message received from a client
        /// </summary>
        private async Task ProcessMessageAsync(string messageJson, TcpClient client)
        {
            try
            {
                var message = JsonSerializer.Deserialize<NetworkMessage>(messageJson);
                
                if (message == null)
                    return;

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
                        _eventManager.NotifyLaunchJobsRequested(jobNamesStart);
                        break;
                    
                    case NetworkMessage.MessageTypes.PauseJob:
                        var jobNamesPause = message.GetData<List<string>>();
                        _eventManager.NotifyPauseJobsRequested(jobNamesPause);
                        break;
                    
                    case NetworkMessage.MessageTypes.ResumeJob:
                        var jobNamesResume = message.GetData<List<string>>();
                        _eventManager.NotifyResumeJobsRequested(jobNamesResume);
                        break;
                    
                    case NetworkMessage.MessageTypes.StopJob:
                        var jobNamesStop = message.GetData<List<string>>();
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
            try
            {
                // Get all jobs from the backup manager
                var jobs = _backupManager.GetAllJobs();
                
                // Create job status snapshots
                var jobStates = new List<JobState>();
                foreach (var job in jobs)
                {
                    var snapshot = job.Status.CreateSnapshot();
                    jobStates.Add(snapshot);
                }
                
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
                // Create a state snapshot of the job
                var jobState = status.CreateSnapshot();
                var message = NetworkMessage.CreateJobStatusMessage(new List<JobState> { jobState });

                // Broadcast to all clients
                await BroadcastMessageAsync(message).ConfigureAwait(false);
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
            if (message == null || client == null || !client.Connected)
                return;

            try
            {
                var messageJson = JsonSerializer.Serialize(message);
                var buffer = Encoding.UTF8.GetBytes(messageJson);
                
                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending message to client: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts a message to all connected clients
        /// </summary>
        private async Task BroadcastMessageAsync(NetworkMessage message)
        {
            if (message == null || !IsRunning)
                return;

            var disconnectedClients = new List<string>();

            foreach (var kvp in connectedClients)
            {
                TcpClient client = kvp.Value;
                
                if (!IsClientConnected(client))
                {
                    disconnectedClients.Add(kvp.Key);
                    continue;
                }

                try
                {
                    await SendMessageToClientAsync(message, client).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error broadcasting to client {kvp.Key}: {ex.Message}");
                    disconnectedClients.Add(kvp.Key);
                }
            }

            // Clean up any disconnected clients
            foreach (var id in disconnectedClients)
            {
                if (connectedClients.TryRemove(id, out var client))
                {
                    try
                    {
                        client.Close();
                    }
                    catch { /* Ignore errors when cleaning up */ }
                }
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
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch
            {
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