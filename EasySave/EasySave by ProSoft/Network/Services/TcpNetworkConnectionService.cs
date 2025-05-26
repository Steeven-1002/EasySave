using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EasySave_by_ProSoft.Network.Services
{
    /// <summary>
    /// TCP implementation of the network connection service
    /// </summary>
    public class TcpNetworkConnectionService : INetworkConnectionService
    {
        private TcpClient? _client;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isConnected;
        private bool _isOperationInProgress;
        private bool _isDisposed;
        
        public bool IsConnected => _isConnected;
        public bool IsOperationInProgress => _isOperationInProgress;
        
        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<string>? StatusMessageChanged;
        public event EventHandler<List<JobState>>? JobStatusesUpdated;
        public event EventHandler<bool>? BusinessSoftwareStateChanged;
        
        /// <summary>
        /// Connects to the server using the specified address and port
        /// </summary>
        public async Task ConnectAsync(string serverAddress, int serverPort)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(TcpNetworkConnectionService));
            if (IsConnected || IsOperationInProgress) return;
            
            SetOperationInProgress(true);
            UpdateConnectionStatus("Connecting...");
            UpdateStatusMessage("Connecting to server...");
            
            try
            {
                CleanupClient();
                
                _client = new TcpClient();
                var connectTask = _client.ConnectAsync(serverAddress, serverPort);
                
                // Add timeout to connection attempt
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                {
                    throw new TimeoutException("Connection attempt timed out");
                }
                
                await connectTask;
                
                if (_client == null || !_client.Connected)
                {
                    throw new InvalidOperationException("Failed to establish connection");
                }
                
                _isConnected = true;
                UpdateConnectionStatus($"Connected to {serverAddress}:{serverPort}");
                UpdateStatusMessage("Connected successfully");
                
                _cancellationTokenSource = new CancellationTokenSource();
                _ = ListenForMessagesAsync(_cancellationTokenSource.Token);
            }
            catch (SocketException ex)
            {
                _isConnected = false;
                UpdateConnectionStatus($"Connection failed: {ex.Message}");
                UpdateStatusMessage($"Connection failed: {ex.Message}");
                CleanupClient();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                UpdateConnectionStatus($"Connection failed: {ex.Message}");
                UpdateStatusMessage($"Connection failed: {ex.Message}");
                CleanupClient();
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }
        
        /// <summary>
        /// Disconnects from the server
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(TcpNetworkConnectionService));
            if (!IsConnected || IsOperationInProgress) return;
            
            SetOperationInProgress(true);
            try
            {
                // Cancel the token first
                _cancellationTokenSource?.Cancel();
                
                if (_client != null && _client.Connected)
                {
                    try
                    {
                        NetworkStream stream = _client.GetStream();
                        // Use a shorter timeout to avoid hanging
                        await Task.Run(() => stream.Close(1000));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error closing stream gracefully: {ex.Message}");
                    }
                }
                
                UpdateStatusMessage("Disconnected from server");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during disconnect: {ex.Message}");
                UpdateStatusMessage($"Error during disconnect: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
                UpdateConnectionStatus("Disconnected");
                CleanupClient();
                SetOperationInProgress(false);
            }
        }
        
        /// <summary>
        /// Requests job status update from the server
        /// </summary>
        public async Task RequestJobStatusUpdateAsync()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(TcpNetworkConnectionService));
            
            // Enhanced validation with more detailed logging
            if (!IsConnected || _client == null || !_client.Connected)
            {
                if (IsConnected)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RequestJobStatusUpdate detected disconnected socket while IsConnected=true");
                    HandleConnectionLoss();
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RequestJobStatusUpdate called but client is not connected");
                }
                return;
            }
            
            try
            {
                // Create command with explicit CommandType and add timestamp for tracking
                var command = new RemoteCommand
                {
                    CommandType = "RequestStatusUpdate",
                    Parameters = new Dictionary<string, object> { { "Timestamp", DateTime.Now.ToString("o") } }
                };
                
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Requesting job status update...");
                await SendCommandAsync(command);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Job status update requested (CommandType: {command.CommandType})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error requesting job status update: {ex.Message}");
                UpdateStatusMessage($"Error requesting update: {ex.Message}");
                
                // Check if the connection is still valid after the error
                if (_client != null && !_client.Connected && IsConnected)
                {
                    HandleConnectionLoss();
                }
            }
        }
        
        /// <summary>
        /// Sends a command to the server
        /// </summary>
        public async Task SendCommandAsync(RemoteCommand command)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(TcpNetworkConnectionService));
            
            // Early validation with enhanced logging
            if (!IsConnected || _client == null || !_client.Connected)
            {
                if (IsConnected)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SendCommandAsync detected disconnected socket while IsConnected=true");
                    HandleConnectionLoss();
                }
                return;
            }
            
            // Validate CommandType
            if (string.IsNullOrWhiteSpace(command.CommandType))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SendCommandAsync: CommandType is empty. Command will not be sent.");
                UpdateStatusMessage("Error: Attempted to send a command with an empty CommandType.");
                return;
            }
            
            SetOperationInProgress(true);
            try
            {
                string json = JsonSerializer.Serialize(command);
                byte[] data = Encoding.UTF8.GetBytes(json);
                
                // Check again right before accessing the stream
                if (_client == null || !_client.Connected)
                {
                    throw new InvalidOperationException("Socket disconnected before sending command");
                }
                
                using NetworkStream stream = _client.GetStream();
                
                // Create a Task with timeout
                CancellationTokenSource timeoutCts = new CancellationTokenSource(5000); // 5 second timeout
                try
                {
                    await stream.WriteAsync(data, timeoutCts.Token);
                    await stream.FlushAsync(timeoutCts.Token);
                    
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sent command: {command.CommandType} for {command.JobNames?.Count ?? 0} jobs");
                    
                    string messageType = command.CommandType switch
                    {
                        "LaunchJobs" => "Start",
                        "PauseJobs" => "Pause",
                        "ResumeJobs" => "Resume",
                        "StopJobs" => "Stop",
                        "RequestStatusUpdate" => "Update Request",
                        _ => "Command"
                    };
                    
                    if (command.CommandType != "RequestStatusUpdate")
                    {
                        UpdateStatusMessage($"{messageType} command sent to server");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"Command {command.CommandType} send operation timed out after 5 seconds");
                }
                finally
                {
                    timeoutCts.Dispose();
                }
            }
            catch (TimeoutException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Command timed out: {ex.Message}");
                UpdateConnectionStatus("Connection timeout");
                UpdateStatusMessage("Connection timeout - command not sent");
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket disposed: {ex.Message}");
                UpdateConnectionStatus("Connection closed");
                UpdateStatusMessage("Connection closed - command not sent");
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Invalid socket operation: {ex.Message}");
                UpdateConnectionStatus("Connection invalid");
                UpdateStatusMessage("Connection invalid - command not sent");
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket error: {ex.Message}");
                
                if (IsGracefulDisconnect(ex))
                {
                    UpdateConnectionStatus("Server closed connection");
                    UpdateStatusMessage("Server closed the connection");
                }
                else
                {
                    UpdateConnectionStatus($"Socket error: {ex.Message}");
                    UpdateStatusMessage($"Socket error - command not sent");
                }
                
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error sending command: {ex.Message}");
                UpdateConnectionStatus($"Error sending command: {ex.Message}");
                UpdateStatusMessage($"Error sending command: {ex.Message}");
                
                if (_client == null || !_client.Connected)
                {
                    await CleanupClientResourcesAsync();
                    HandleConnectionLoss();
                }
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public Task SendCommandAsync(Models.RemoteCommand cmd)
        {
            // Convert from Models.RemoteCommand to Network.RemoteCommand
            var command = new RemoteCommand(cmd.CommandType)
            {
                JobNames = cmd.JobNames,
                Parameters = cmd.Parameters
            };
            
            return SendCommandAsync(command);
        }

        #region Private Methods
        /// <summary>
        /// Listens for messages from the server
        /// </summary>
        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_client == null || !_client.Connected)
                {
                    throw new InvalidOperationException("Cannot receive messages from a disconnected socket");
                }
                
                using NetworkStream stream = _client.GetStream();
                byte[] buffer = new byte[16384]; // Larger buffer for potentially large job lists
                DateTime lastActivityTime = DateTime.Now;
                bool connectionCheckPending = false;
                
                while (!cancellationToken.IsCancellationRequested && _client != null && _client.Connected)
                {
                    try
                    {
                        if (stream.DataAvailable)
                        {
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                            if (bytesRead > 0)
                            {
                                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                ProcessServerMessage(json);
                                lastActivityTime = DateTime.Now;
                                connectionCheckPending = false;
                            }
                            else if (bytesRead == 0)
                            {
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received zero bytes - connection closed by server");
                                throw new SocketException((int)SocketError.ConnectionReset);
                            }
                        }
                        else
                        {
                            // If no activity for more than 5 seconds, check connection with a ping
                            if (!connectionCheckPending && DateTime.Now - lastActivityTime > TimeSpan.FromSeconds(5))
                            {
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No activity for 5 seconds, checking connection");
                                // Check connection health
                                connectionCheckPending = true;
                                if (!await CheckConnectionAsync())
                                {
                                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Connection check failed");
                                    throw new SocketException((int)SocketError.Disconnecting);
                                }
                                
                                // Send a keep alive request to server
                                _ = Task.Run(async () => {
                                    try {
                                        var keepAliveCmd = new RemoteCommand("KeepAlive");
                                        await SendCommandAsync(keepAliveCmd);
                                        lastActivityTime = DateTime.Now;
                                    }
                                    catch (Exception ex) {
                                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Keep alive failed: {ex.Message}");
                                    }
                                    finally {
                                        connectionCheckPending = false;
                                    }
                                });
                            }
                            
                            await Task.Delay(100, cancellationToken);
                            
                            // Regular check if client is still connected
                            if (_client == null || !_client.Connected)
                            {
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Periodic check found socket disconnected");
                                throw new SocketException((int)SocketError.Disconnecting);
                            }
                        }
                    }
                    catch (System.IO.IOException ex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket IO Exception: {ex.Message}");
                        throw;
                    }
                    catch (SocketException ex)
                    {
                        if (IsGracefulDisconnect(ex))
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server initiated graceful disconnect: {ex.Message}");
                        }
                        else
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket Exception during receive: {ex.Message}");
                        }
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Receive messages operation was canceled");
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Network stream or client was disposed");
                await CleanupClientResourcesAsync();
            }
            catch (SocketException ex)
            {
                if (IsGracefulDisconnect(ex))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Graceful socket disconnection detected: {ex.Message}");
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket error: {ex.Message}");
                }
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error receiving messages: {ex.Message}");
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
        }
        
        /// <summary>
        /// Processes a message received from the server
        /// </summary>
        private void ProcessServerMessage(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received empty message from server");
                    return;
                }

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Processing server message: {json.Length} bytes");
                
                using var document = JsonDocument.Parse(json);
                string? messageType = null;
                if (document.RootElement.TryGetProperty("Type", out var typeElement))
                {
                    messageType = typeElement.GetString();
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Message type: {messageType}");
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Missing 'Type' property in server message.");
                    return;
                }

                switch (messageType)
                {
                    case "JobStatuses":
                        if (document.RootElement.TryGetProperty("Data", out var dataElement))
                        {
                            ProcessJobStatuses(dataElement);
                        }
                        break;

                    case "BusinessAppState":
                        if (document.RootElement.TryGetProperty("IsRunning", out var isRunningElement))
                        {
                            bool isRunning = isRunningElement.GetBoolean();
                            BusinessSoftwareStateChanged?.Invoke(this, isRunning);
                        }
                        break;

                    case "CommandResponse":
                        if (document.RootElement.TryGetProperty("Status", out JsonElement statusElement))
                        {
                            string status = statusElement.GetString() ?? string.Empty;
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received command response: {status}");
                            UpdateStatusMessage(status);
                        }
                        break;
                        
                    case "ConnectionCheck":
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received connection check from server");
                        // Respond to keep connection alive
                        _ = Task.Run(async () => {
                            try {
                                var response = new RemoteCommand("ConnectionCheckResponse");
                                await SendCommandAsync(response);
                            } 
                            catch (Exception ex) {
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Failed to respond to connection check: {ex.Message}");
                            }
                        });
                        break;
                    
                    case "KeepAliveResponse":
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received keep alive response from server");
                        break;

                    default:
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unknown message type: {messageType}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Malformed JSON from server: {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JSON content: {json}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error processing message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Processes job status updates from the server
        /// </summary>
        private void ProcessJobStatuses(JsonElement jobStatesElement)
        {
            try
            {
                if (jobStatesElement.ValueKind != JsonValueKind.Array)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JobStatuses Data is not an array. Value kind: {jobStatesElement.ValueKind}");
                    return;
                }

                // Log the raw JSON to examine its structure
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Processing job states: {jobStatesElement.GetArrayLength()} items");

                var jobStates = JsonSerializer.Deserialize<List<JobState>>(jobStatesElement.GetRawText());
                
                if (jobStates == null)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Deserialized job states is null");
                    return;
                }

                // Filter out any invalid job states
                var validJobStates = jobStates.FindAll(js => js != null && !string.IsNullOrEmpty(js.JobName));
                
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Found {validJobStates.Count} valid job states out of {jobStates.Count} total");
                
                // Only raise event if there are valid jobs to report
                if (validJobStates.Count > 0)
                {
                    // Log each job state for debugging
                    foreach (var job in validJobStates)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Job: {job.JobName}, State: {job.State}, Progress: {job.ProgressPercentage}%");
                    }
                    
                    JobStatusesUpdated?.Invoke(this, validJobStates);
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JobStatusesUpdated event raised with {validJobStates.Count} job states");
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No valid job states found, event not raised");
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error deserializing job states: {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Raw JSON: {jobStatesElement.GetRawText()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error processing job statuses: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determines if a socket exception represents a graceful disconnection
        /// </summary>
        private bool IsGracefulDisconnect(SocketException ex)
        {
            return ex.SocketErrorCode == SocketError.ConnectionAborted ||
                   ex.SocketErrorCode == SocketError.ConnectionReset ||
                   ex.SocketErrorCode == SocketError.Disconnecting ||
                   ex.SocketErrorCode == SocketError.Shutdown ||
                   ex.Message.Contains("shutdown", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("closed by the remote host", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Handles connection loss events
        /// </summary>
        private void HandleConnectionLoss()
        {
            if (!_isConnected) return;
            
            _isConnected = false;
            UpdateConnectionStatus("Connection lost");
            UpdateStatusMessage("Connection to server was lost");
            CleanupClient();
        }
        
        /// <summary>
        /// Cleans up client resources
        /// </summary>
        private void CleanupClient()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                if (_client != null)
                {
                    _client.Close();
                    _client.Dispose();
                    _client = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during client cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely cleans up client resources asynchronously
        /// </summary>
        private async Task CleanupClientResourcesAsync()
        {
            if (_client == null) return;
            
            try
            {
                // Try to gracefully close the stream if available
                if (_client.Connected && _client.GetStream() != null)
                {
                    try
                    {
                        NetworkStream stream = _client.GetStream();
                        var closeTask = Task.Run(() => stream.Close(500));
                        
                        if (await Task.WhenAny(closeTask, Task.Delay(1000)) != closeTask)
                        {
                            Debug.WriteLine("Stream close timed out");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during stream cleanup: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during client cleanup: {ex.Message}");
            }
            
            // Always clean up client resources
            CleanupClient();
        }
        
        /// <summary>
        /// Updates the connection status and notifies subscribers
        /// </summary>
        private void UpdateConnectionStatus(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }
        
        /// <summary>
        /// Updates the status message and notifies subscribers
        /// </summary>
        private void UpdateStatusMessage(string message)
        {
            StatusMessageChanged?.Invoke(this, message);
        }
        
        /// <summary>
        /// Sets the operation in progress flag
        /// </summary>
        private void SetOperationInProgress(bool inProgress)
        {
            _isOperationInProgress = inProgress;
        }
        
        /// <summary>
        /// Checks the connection health
        /// </summary>
        private async Task<bool> CheckConnectionAsync()
        {
            if (_client == null) return false;
            
            try
            {
                if (!_client.Connected)
                    return false;
                    
                // Method 1: Use Socket.Poll to check connection status
                if (_client.Client.Poll(1, SelectMode.SelectRead) && _client.Client.Available == 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket.Poll indicates the connection is closed");
                    return false;
                }
                
                // Method 2: Check if we can get the network stream (will throw if disconnected)
                try
                {
                    var stream = _client.GetStream();
                    if (stream == null)
                        return false;
                }
                catch
                {
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error checking connection: {ex.Message}");
                return false;
            }
        }
        #endregion
        
        #region IDisposable Implementation
        /// <summary>
        /// Disposes of the network connection service
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Disposes of the network connection service
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            
            if (disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _client?.Close();
                _client?.Dispose();
                
                _cancellationTokenSource = null;
                _client = null;
            }
            
            _isDisposed = true;
        }
        #endregion
    }
}