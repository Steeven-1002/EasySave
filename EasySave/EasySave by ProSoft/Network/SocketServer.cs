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

            lock (_connectedClients)
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

                        lock (_connectedClients)
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
            string clientAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            try
            {
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[4096];

                // Send initial job status list to the client
                await SendJobStatuses(client);

                while (!token.IsCancellationRequested && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            ProcessClientMessage(message, clientAddress);
                        }
                    }
                    else
                    {
                        await Task.Delay(100, token);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling client {clientAddress}: {ex.Message}");
            }
            finally
            {
                lock (_connectedClients)
                {
                    _connectedClients.Remove(client);
                }
                client.Close();
                ClientDisconnected?.Invoke(this, clientAddress);
                Debug.WriteLine($"Client {clientAddress} disconnected");
            }
        }

        private void ProcessClientMessage(string message, string clientAddress)
        {
            try
            {
                var command = JsonSerializer.Deserialize<RemoteCommand>(message);
                if (command != null)
                {
                    Debug.WriteLine($"Received command: {command.CommandType} from {clientAddress}");
                    CommandReceived?.Invoke(this, command);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing message from {clientAddress}: {ex.Message}");
            }
        }

        public async Task BroadcastJobStatusesAsync()
        {
            if (!_isRunning || _connectedClients.Count == 0)
                return;

            List<TcpClient> clientsCopy;
            lock (_connectedClients)
            {
                clientsCopy = _connectedClients.ToList();
            }

            foreach (var client in clientsCopy)
            {
                try
                {
                    await SendJobStatuses(client);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error broadcasting job statuses: {ex.Message}");
                }
            }
        }

        private async Task SendJobStatuses(TcpClient client)
        {
            if (!client.Connected) return;

            try
            {
                // Create a snapshot of all job states to send
                var jobStates = JobEventManager.Instance.GetAllJobStates();
                var response = new
                {
                    Type = "JobStatuses",
                    Data = jobStates
                };

                string json = JsonSerializer.Serialize(response);
                byte[] data = Encoding.UTF8.GetBytes(json);

                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                Debug.WriteLine($"Sent job statuses to client: {jobStates.Count} states");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending job statuses to client: {ex.Message}");
                throw;
            }
        }

        public async Task BroadcastBusinessAppStateAsync(bool isRunning)
        {
            if (!_isRunning || _connectedClients.Count == 0)
                return;

            var response = new
            {
                Type = "BusinessAppState",
                IsRunning = isRunning
            };

            string json = JsonSerializer.Serialize(response);
            byte[] data = Encoding.UTF8.GetBytes(json);

            List<TcpClient> clientsCopy;
            lock (_connectedClients)
            {
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
            CommandType = string.Empty;
            JobName = string.Empty;
            JobNames = new List<string>();
            Parameters = new Dictionary<string, object>();
        }
    }
}