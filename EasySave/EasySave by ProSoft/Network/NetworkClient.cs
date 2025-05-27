using EasySave_by_ProSoft.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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
        /// Disconnects from the server
        /// </summary>
        public void Disconnect()
        {
            lock (connectionLock)
            {
                if (!isRunning)
                    return;

                isRunning = false;
                StopPingTimer();

                try
                {
                    // Cancel any ongoing operations
                    cancellationTokenSource?.Cancel();
                    
                    // Close the stream
                    stream?.Close();
                    stream = null;
                    
                    // Close the client
                    client?.Close();
                    client = null;
                    
                    OnConnectionStatusChanged("Disconnected");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during disconnect: {ex.Message}");
                }
                finally
                {
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }
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
            if (!AutoReconnect || IsConnected)
                return;

            try
            {
                await reconnectSemaphore.WaitAsync();
                
                if (IsConnected) // Double-check after acquiring semaphore
                    return;

                OnConnectionStatusChanged("Connection lost. Attempting to reconnect...");
                
                // Clean up old connection
                Disconnect();
                
                // Wait before reconnecting
                await Task.Delay(2000).ConfigureAwait(false);
                
                // Try to connect
                await ConnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during reconnect: {ex.Message}");
                OnErrorOccurred(ex);
            }
            finally
            {
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
                var message = JsonSerializer.Deserialize<NetworkMessage>(messageJson);
                if (message == null)
                    return;

                // Raise generic message received event
                OnMessageReceived(message);

                // Handle specific message types
                switch (message.Type)
                {
                    case NetworkMessage.MessageTypes.JobStatus:
                        var jobStates = message.GetData<List<JobState>>();
                        if (jobStates != null)
                            OnJobStatusesReceived(jobStates);
                        break;
                        
                    case NetworkMessage.MessageTypes.Pong:
                        // Ping response received, connection healthy
                        break;
                        
                    case NetworkMessage.MessageTypes.Error:
                        var errorMessage = message.GetData<string>();
                        OnConnectionStatusChanged($"Server error: {errorMessage}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                OnErrorOccurred(new Exception($"Invalid message format: {ex.Message}", ex));
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
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
                var messageJson = JsonSerializer.Serialize(message);
                var buffer = Encoding.UTF8.GetBytes(messageJson);

                await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
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
        /// Sends a request for current job statuses
        /// </summary>
        public async Task RequestJobStatusesAsync()
        {
            await SendMessageAsync(NetworkMessage.CreateJobStatusRequestMessage()).ConfigureAwait(false);
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