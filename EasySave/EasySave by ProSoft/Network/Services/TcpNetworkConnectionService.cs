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
            
            // Validate connection state
            if (!IsConnected || _client == null || !_client.Connected)
            {
                if (IsConnected)
                {
                    Debug.WriteLine("RequestJobStatusUpdate detected disconnected socket while IsConnected=true");
                    HandleConnectionLoss();
                }
                return;
            }
            
            try
            {
                // Ensure CommandType is set
                var command = new RemoteCommand();
                await SendCommandAsync(command);
                Debug.WriteLine($"Job status update requested (CommandType: {command.CommandType})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error requesting job status update: {ex.Message}");
                UpdateStatusMessage($"Error requesting update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sends a command to the server
        /// </summary>
        public async Task SendCommandAsync(RemoteCommand command)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(TcpNetworkConnectionService));
            
            // Early validation
            if (!IsConnected || _client == null || !_client.Connected)
            {
                if (IsConnected)
                {
                    Debug.WriteLine("SendCommandAsync detected disconnected socket while IsConnected=true");
                    HandleConnectionLoss();
                }
                return;
            }
            
            // Validate CommandType
            if (string.IsNullOrWhiteSpace(command.CommandType))
            {
                Debug.WriteLine("SendCommandAsync: CommandType is empty. Command will not be sent.");
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
                await stream.WriteAsync(data);
                await stream.FlushAsync();
                
                Debug.WriteLine($"Sent command: {command.CommandType} for {command.JobNames?.Count ?? 0} jobs");
                
                if (command.CommandType != "RequestStatusUpdate")
                {
                    UpdateStatusMessage($"{command.CommandType} command sent to server");
                }
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"Socket disposed: {ex.Message}");
                UpdateConnectionStatus("Connection closed");
                UpdateStatusMessage("Connection closed - command not sent");
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Invalid socket operation: {ex.Message}");
                UpdateConnectionStatus("Connection invalid");
                UpdateStatusMessage("Connection invalid - command not sent");
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"Socket error: {ex.Message}");
                
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
                Debug.WriteLine($"Error sending command: {ex.Message}");
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
                byte[] buffer = new byte[8192]; // Large buffer for potentially large job lists
                
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
                            }
                            else if (bytesRead == 0)
                            {
                                Debug.WriteLine("Received zero bytes - connection closed by server");
                                throw new SocketException((int)SocketError.ConnectionReset);
                            }
                        }
                        else
                        {
                            await Task.Delay(100, cancellationToken);
                            
                            // Check if client is still connected
                            if (_client == null || !_client.Connected)
                            {
                                Debug.WriteLine("Periodic check found socket disconnected");
                                throw new SocketException((int)SocketError.Disconnecting);
                            }
                        }
                    }
                    catch (System.IO.IOException ex)
                    {
                        Debug.WriteLine($"Socket IO Exception: {ex.Message}");
                        throw;
                    }
                    catch (SocketException ex)
                    {
                        if (IsGracefulDisconnect(ex))
                        {
                            Debug.WriteLine($"Server initiated graceful disconnect: {ex.Message}");
                        }
                        else
                        {
                            Debug.WriteLine($"Socket Exception during receive: {ex.Message}");
                        }
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Receive messages operation was canceled");
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine("Network stream or client was disposed");
                await CleanupClientResourcesAsync();
            }
            catch (SocketException ex)
            {
                if (IsGracefulDisconnect(ex))
                {
                    Debug.WriteLine($"Graceful socket disconnection detected: {ex.Message}");
                }
                else
                {
                    Debug.WriteLine($"Socket error: {ex.Message}");
                }
                await CleanupClientResourcesAsync();
                HandleConnectionLoss();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error receiving messages: {ex.Message}");
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
                    return;

                using var document = JsonDocument.Parse(json);
                string? messageType = null;
                if (document.RootElement.TryGetProperty("Type", out var typeElement))
                {
                    messageType = typeElement.GetString();
                }
                else
                {
                    Debug.WriteLine("Missing 'Type' property in server message.");
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
                            UpdateStatusMessage(statusElement.GetString() ?? string.Empty);
                        }
                        break;

                    default:
                        Debug.WriteLine($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Malformed JSON from server: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing message: {ex.Message}");
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
                    Debug.WriteLine("JobStatuses Data is not an array.");
                    return;
                }

                var jobStates = JsonSerializer.Deserialize<List<JobState>>(jobStatesElement.GetRawText());
                if (jobStates == null) return;

                // Filter out any invalid job states
                jobStates = jobStates.FindAll(js => js != null && !string.IsNullOrEmpty(js.JobName));

                JobStatusesUpdated?.Invoke(this, jobStates);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Error deserializing job states: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing job statuses: {ex.Message}");
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

        public Task SendCommandAsync(Models.RemoteCommand cmd)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}