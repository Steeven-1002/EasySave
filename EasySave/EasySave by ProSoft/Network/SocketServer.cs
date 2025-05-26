using EasySave_by_ProSoft.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace EasySave_by_ProSoft.Network
{
    /// <summary>
    /// Server that handles socket communications with remote clients for control and monitoring
    /// </summary>
    public class SocketServer
    {
        private TcpListener _server;
        private readonly List<TcpClient> _connectedClients = new();
        private CancellationTokenSource _cancellationTokenSource;
        private readonly int _port;
        private bool _isRunning;
        private readonly object _clientsLock = new object();

        public bool IsRunning => _isRunning;
        public event EventHandler<string> ClientConnected;
        public event EventHandler<string> ClientDisconnected;
        public event EventHandler<RemoteCommand> CommandReceived;

        public SocketServer(int port = 55555)
        {
            _port = port;
        }

        public void Start()
        {
            if (_isRunning) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _server = new TcpListener(IPAddress.Any, _port);

            Task.Run(() => RunServer(_cancellationTokenSource.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cancellationTokenSource?.Cancel();
            _server?.Stop();
            _isRunning = false;

            lock (_clientsLock)
            {
                foreach (var client in _connectedClients)
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
                _connectedClients.Clear();
            }
        }

        private async Task RunServer(CancellationToken token)
        {
            try
            {
                _server.Start();
                _isRunning = true;
                Debug.WriteLine($"Socket server started on port {_port}");

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _server.AcceptTcpClientAsync();
                        string clientAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                        Debug.WriteLine($"Client connected from {clientAddress}");

                        lock (_clientsLock)
                        {
                            _connectedClients.Add(client);
                        }

                        ClientConnected?.Invoke(this, clientAddress);

                        // Start handling client in the background
                        _ = Task.Run(async () => await HandleClientAsync(client, token), token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when token is canceled
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error accepting client: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Server error: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                Debug.WriteLine("Socket server stopped");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            if (client == null) return;
            
            string clientAddress = "unknown";
            try 
            {
                clientAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            }
            catch {}

            try
            {
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];

                // Send initial job status list to the client
                try
                {
                    await SendJobStatuses(client);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error sending initial job statuses to {clientAddress}: {ex.Message}");
                    // Continue with handling - don't exit on initial status send failure
                }

                // Send keep-alive messages periodically to ensure connection
                var keepAliveTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                _ = Task.Run(async () => await SendPeriodicUpdates(client, keepAliveTokenSource.Token), token);

                while (!token.IsCancellationRequested && client.Connected)
                {
                    try
                    {
                        if (stream.DataAvailable)
                        {
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                            if (bytesRead > 0)
                            {
                                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                ProcessClientMessage(message, client, clientAddress);
                            }
                        }
                        else
                        {
                            await Task.Delay(100, token);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in client communication loop for {clientAddress}: {ex.Message}");
                        break; // Exit the loop on error
                    }
                }

                // Cancel the keep-alive task when client handler exits
                keepAliveTokenSource.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling client {clientAddress}: {ex.Message}");
            }
            finally
            {
                lock (_clientsLock)
                {
                    _connectedClients.Remove(client);
                }
                try
                {
                    client.Close();
                }
                catch {}
                ClientDisconnected?.Invoke(this, clientAddress);
                Debug.WriteLine($"Client {clientAddress} disconnected");
            }
        }

        private async Task SendPeriodicUpdates(TcpClient client, CancellationToken token)
        {
            try
            {
                int heartbeatCounter = 0;
                int backoffDelay = 5000; // Start with 5 seconds
                
                while (!token.IsCancellationRequested && client.Connected)
                {
                    await Task.Delay(backoffDelay, token); // Adaptive delay
                    
                    if (!client.Connected) break;
                    
                    try
                    {
                        // Every third cycle, also send a connection check
                        if (++heartbeatCounter % 3 == 0)
                        {
                            await SendConnectionCheck(client);
                        }
                        
                        await SendJobStatuses(client);
                        
                        // If success, reset backoff delay
                        backoffDelay = 5000;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error sending periodic update: {ex.Message}");
                        
                        // If client is no longer connected, break the loop
                        if (!client.Connected)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client disconnected during periodic update");
                            break;
                        }
                        
                        // Increase backoff delay for exponential backoff, cap at 15 seconds
                        backoffDelay = Math.Min(backoffDelay * 2, 15000);
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Setting backoff delay to {backoffDelay}ms");
                        
                        // Wait a shorter time before retry on error
                        await Task.Delay(1000, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when token is canceled
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Periodic updates canceled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error in periodic updates: {ex.Message}");
            }
        }

        private void ProcessClientMessage(string message, TcpClient sender, string clientAddress)
        {
            try
            {
                // Detailed logging
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received message from {clientAddress}: {message.Length} bytes");
                
                var command = JsonSerializer.Deserialize<RemoteCommand>(message);
                if (command != null)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received command: {command.CommandType} from {clientAddress}");

                    // Handle different command types
                    switch (command.CommandType)
                    {
                        case "RequestStatusUpdate":
                            _ = ProcessStatusUpdateRequest(sender, command);
                            return;
                        
                        case "ConnectionCheckResponse":
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received connection check response from {clientAddress}");
                            return;
                            
                        case "KeepAlive":
                            // Send keep alive response
                            _ = Task.Run(async () => {
                                try {
                                    var response = new {
                                        Type = "KeepAliveResponse",
                                        Timestamp = DateTime.Now
                                    };
                                    
                                    string json = JsonSerializer.Serialize(response);
                                    byte[] data = Encoding.UTF8.GetBytes(json);
                                    
                                    NetworkStream stream = sender.GetStream();
                                    await stream.WriteAsync(data, 0, data.Length);
                                    await stream.FlushAsync();
                                }
                                catch (Exception ex) {
                                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error sending keep alive response: {ex.Message}");
                                }
                            });
                            return;

                        default:
                            // For all other commands, use the standard event handling
                            CommandReceived?.Invoke(this, command);
                            
                            // Send acknowledgment to client
                            _ = SendCommandAcknowledgement(sender, command);
                            
                            // Also send updated job states after a short delay to allow processing
                            _ = Task.Run(async () => 
                            {
                                try
                                {
                                    await Task.Delay(500); // Give time for the command to be processed
                                    await SendJobStatuses(sender);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error sending job statuses after command: {ex.Message}");
                                }
                            });
                            break;
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error parsing JSON from {clientAddress}: {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Raw message: {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error processing message from {clientAddress}: {ex.Message}");
            }
        }

        private async Task SendCommandAcknowledgement(TcpClient client, RemoteCommand command)
        {
            if (client == null || !client.Connected) return;

            try
            {
                var response = new
                {
                    Type = "CommandResponse",
                    CommandType = command.CommandType,
                    Status = $"{command.CommandType} command received and processed"
                };

                string json = JsonSerializer.Serialize(response);
                byte[] data = Encoding.UTF8.GetBytes(json);

                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                Debug.WriteLine($"Sent command acknowledgement for {command.CommandType}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending command acknowledgement: {ex.Message}");
            }
        }

        private async Task ProcessStatusUpdateRequest(TcpClient client, RemoteCommand command)
        {
            if (client == null || !client.Connected) return;

            try
            {
                // Send current job statuses
                await SendJobStatuses(client);

                // Also send a command response confirming receipt
                var response = new
                {
                    Type = "CommandResponse",
                    Status = "Status update request processed"
                };

                string json = JsonSerializer.Serialize(response);
                byte[] data = Encoding.UTF8.GetBytes(json);

                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing status update request: {ex.Message}");
            }
        }

        public async Task BroadcastJobStatusesAsync()
        {
            if (!_isRunning) return;

            List<TcpClient> clientsCopy;
            lock (_clientsLock)
            {
                if (_connectedClients.Count == 0) return;
                clientsCopy = _connectedClients.ToList();
            }

            foreach (var client in clientsCopy)
            {
                try
                {
                    if (client.Connected)
                    {
                        await SendJobStatuses(client);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error broadcasting job statuses: {ex.Message}");
                }
            }
        }

        private async Task SendJobStatuses(TcpClient client)
        {
            if (client == null || !client.Connected) return;

            try
            {
                // Create a snapshot of all job states to send
                var jobStates = JobEventManager.Instance.GetAllJobStates();
                
                // Ensure we don't have null or invalid job states
                jobStates = jobStates?.Where(js => js != null && !string.IsNullOrEmpty(js.JobName))
                                  .ToList() ?? new List<JobState>();
                
                // Log the job states being sent
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Preparing to send {jobStates.Count} job states to client");
                
                var response = new
                {
                    Type = "JobStatuses",
                    Data = jobStates,
                    Timestamp = DateTime.Now
                };

                var options = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false
                };

                string json = JsonSerializer.Serialize(response, options);
                byte[] data = Encoding.UTF8.GetBytes(json);

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sending {data.Length} bytes of job status data");
                
                // Check connection again before trying to send
                if (!client.Connected)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Client disconnected before sending job statuses");
                    throw new SocketException((int)SocketError.NotConnected);
                }
                
                // Break up large payloads into smaller chunks if needed
                if (data.Length > 4096)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Using chunked sending for large job status payload ({data.Length} bytes)");
                    await SendDataInChunks(client, data);
                }
                else
                {
                    // Send with timeout protection
                    using (var timeoutCts = new CancellationTokenSource(5000)) // 5-second timeout
                    {
                        try
                        {
                            NetworkStream stream = client.GetStream();
                            await stream.WriteAsync(data, 0, data.Length, timeoutCts.Token);
                            await stream.FlushAsync(timeoutCts.Token);
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Successfully sent job statuses: {jobStates.Count} states");
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sending job statuses timed out");
                            throw new TimeoutException("Sending job statuses timed out after 5 seconds");
                        }
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket was disposed while sending job statuses: {ex.Message}");
                throw;
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket error sending job statuses: {ex.Message}");
                throw;
            }
            catch (TimeoutException ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Timeout while sending job statuses: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error sending job statuses to client: {ex.Message}");
                throw;
            }
        }

        private async Task SendDataInChunks(TcpClient client, byte[] data)
        {
            const int chunkSize = 2048;
            int totalBytes = data.Length;
            int bytesSent = 0;
            
            try
            {
                NetworkStream stream = client.GetStream();
                
                while (bytesSent < totalBytes)
                {
                    int bytesToSend = Math.Min(chunkSize, totalBytes - bytesSent);
                    using var timeoutCts = new CancellationTokenSource(3000);
                    await stream.WriteAsync(data, bytesSent, bytesToSend, timeoutCts.Token);
                    bytesSent += bytesToSend;
                    
                    // Report progress
                    if (bytesSent < totalBytes)
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sent chunk: {bytesSent}/{totalBytes} bytes");
                    
                    // Small delay between chunks
                    await Task.Delay(10);
                }
                
                await stream.FlushAsync();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Successfully sent all {totalBytes} bytes in chunks");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error sending chunked data: {ex.Message}. Sent {bytesSent}/{totalBytes} bytes");
                throw;
            }
        }

        private async Task SendConnectionCheck(TcpClient client)
        {
            if (client == null || !client.Connected) return;
            
            try
            {
                var response = new
                {
                    Type = "ConnectionCheck",
                    Timestamp = DateTime.Now
                };
                
                string json = JsonSerializer.Serialize(response);
                byte[] data = Encoding.UTF8.GetBytes(json);
                
                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sent connection check to client");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error sending connection check: {ex.Message}");
                throw;
            }
        }

        public async Task BroadcastBusinessAppStateAsync(bool isRunning)
        {
            if (!_isRunning) return;

            var response = new
            {
                Type = "BusinessAppState",
                IsRunning = isRunning,
                Timestamp = DateTime.Now
            };

            string json = JsonSerializer.Serialize(response);
            byte[] data = Encoding.UTF8.GetBytes(json);

            List<TcpClient> clientsCopy;
            lock (_clientsLock)
            {
                if (_connectedClients.Count == 0) return;
                clientsCopy = _connectedClients.ToList();
            }

            foreach (var client in clientsCopy)
            {
                try
                {
                    if (client.Connected)
                    {
                        NetworkStream stream = client.GetStream();
                        await stream.WriteAsync(data, 0, data.Length);
                        await stream.FlushAsync();
                        Debug.WriteLine($"Sent business app state to client: IsRunning = {isRunning}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error broadcasting business app state: {ex.Message}");
                }
            }
        }
    }

    public class RemoteCommand
    {
        public string CommandType { get; set; }
        public string JobName { get; set; }
        public List<string> JobNames { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public RemoteCommand()
        {
            CommandType = "RequestStatusUpdate"; // Default command type
            JobName = string.Empty;
            JobNames = new List<string>();
            Parameters = new Dictionary<string, object>();
        }
        
        public RemoteCommand(string commandType)
        {
            CommandType = string.IsNullOrWhiteSpace(commandType) ? "RequestStatusUpdate" : commandType;
            JobName = string.Empty;
            JobNames = new List<string>();
            Parameters = new Dictionary<string, object>();
        }
    }
}