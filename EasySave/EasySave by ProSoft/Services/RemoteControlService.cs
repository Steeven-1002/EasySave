using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace EasySave_by_ProSoft.Services
{
    /// <summary>
    /// Service responsible for managing the remote control server lifecycle
    /// Runs independently of UI views
    /// </summary>
    public class RemoteControlService : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        private SocketServer _server;
        private int _serverPort = 9000;
        private string _serverStatus = "Server not running";
        private bool _isServerRunning = false;
        private readonly ObservableCollection<ClientConnectionInfo> _connectedClients;
        private string _localIpAddress;

        public event PropertyChangedEventHandler? PropertyChanged;
        
        // Event for notifying about client connections
        public event EventHandler<ClientConnectionInfo> ClientConnected;
        public event EventHandler<string> ClientDisconnected;
        public event EventHandler<string> ServerStatusChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int ServerPort
        {
            get => _serverPort;
            set
            {
                if (_serverPort != value)
                {
                    _serverPort = value;
                    OnPropertyChanged();
                    
                    // If server is already running, restart it with new port
                    if (IsServerRunning)
                    {
                        StopServer();
                        StartServer();
                    }
                }
            }
        }

        public string ServerStatus
        {
            get => _serverStatus;
            private set
            {
                if (_serverStatus != value)
                {
                    _serverStatus = value;
                    OnPropertyChanged();
                    ServerStatusChanged?.Invoke(this, value);
                }
            }
        }

        public bool IsServerRunning
        {
            get => _isServerRunning;
            private set
            {
                if (_isServerRunning != value)
                {
                    _isServerRunning = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ClientConnectionInfo> ConnectedClients => _connectedClients;

        public string LocalIpAddress
        {
            get
            {
                if (string.IsNullOrEmpty(_localIpAddress))
                {
                    _localIpAddress = GetLocalIPv4Address();
                }
                return _localIpAddress;
            }
        }

        public RemoteControlService(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _connectedClients = new ObservableCollection<ClientConnectionInfo>();
            
            // Initialize the IP address
            _localIpAddress = GetLocalIPv4Address();
            
            // Initialize the socket server
            InitializeSocketServer();
        }

        private void InitializeSocketServer()
        {
            if (_server != null)
                return;

            _server = new SocketServer(_backupManager, ServerPort);
            _server.ServerStatusChanged += Server_StatusChanged;
            _server.MessageReceived += Server_MessageReceived;
        }

        public bool StartServer()
        {
            try
            {
                InitializeSocketServer();

                // Update port if needed
                if (_server.Port != ServerPort)
                {
                    StopServer();
                    _server = new SocketServer(_backupManager, ServerPort);
                    _server.ServerStatusChanged += Server_StatusChanged;
                    _server.MessageReceived += Server_MessageReceived;
                }

                if (_server.Start())
                {
                    IsServerRunning = true;
                    ServerStatus = $"Remote control server running on port {ServerPort}";
                    Debug.WriteLine($"Remote control server started on port {ServerPort}");
                    return true;
                }
                else
                {
                    ServerStatus = $"Failed to start remote control server on port {ServerPort}";
                    Debug.WriteLine($"Failed to start remote control server on port {ServerPort}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting remote control server: {ex.Message}");
                ServerStatus = $"Error starting server: {ex.Message}";
                return false;
            }
        }

        public void StopServer()
        {
            try
            {
                if (_server != null && _server.IsRunning)
                {
                    _server.Stop();
                    IsServerRunning = false;
                    ServerStatus = "Remote control server stopped";
                    _connectedClients.Clear();
                    Debug.WriteLine("Remote control server stopped");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping remote control server: {ex.Message}");
                ServerStatus = $"Error stopping server: {ex.Message}";
            }
        }

        private void Server_StatusChanged(object sender, string status)
        {
            // Update server status
            ServerStatus = status;

            // Update connected clients list
            if (status.StartsWith("Client connected:"))
            {
                var clientInfo = status.Replace("Client connected:", "").Trim();
                var newClient = new ClientConnectionInfo
                {
                    ClientId = Guid.NewGuid().ToString().Substring(0, 8),
                    IpAddress = clientInfo,
                    ConnectedSince = DateTime.Now
                };

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _connectedClients.Add(newClient);
                    ClientConnected?.Invoke(this, newClient);
                });
            }
            else if (status.StartsWith("Client disconnected:"))
            {
                var clientId = status.Replace("Client disconnected:", "").Trim();
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Find and remove the client (in a real implementation, match by ID)
                    if (_connectedClients.Count > 0)
                    {
                        _connectedClients.RemoveAt(_connectedClients.Count - 1);
                        ClientDisconnected?.Invoke(this, clientId);
                    }
                });
            }
        }

        private void Server_MessageReceived(object sender, NetworkMessage message)
        {
            // Handle client messages
            Debug.WriteLine($"Message received: {message.Type}");
            
            // Log more details about the message
            switch (message.Type)
            {
                case NetworkMessage.MessageTypes.JobStatusRequest:
                    Debug.WriteLine("Client requested job status update");
                    break;
                case NetworkMessage.MessageTypes.StartJob:
                    var jobNamesStart = message.GetData<List<string>>();
                    Debug.WriteLine($"Client requested to start jobs: {(jobNamesStart != null ? string.Join(", ", jobNamesStart) : "none")}");
                    break;
                case NetworkMessage.MessageTypes.PauseJob:
                    var jobNamesPause = message.GetData<List<string>>();
                    Debug.WriteLine($"Client requested to pause jobs: {(jobNamesPause != null ? string.Join(", ", jobNamesPause) : "none")}");
                    break;
                case NetworkMessage.MessageTypes.ResumeJob:
                    var jobNamesResume = message.GetData<List<string>>();
                    Debug.WriteLine($"Client requested to resume jobs: {(jobNamesResume != null ? string.Join(", ", jobNamesResume) : "none")}");
                    break;
                case NetworkMessage.MessageTypes.StopJob:
                    var jobNamesStop = message.GetData<List<string>>();
                    Debug.WriteLine($"Client requested to stop jobs: {(jobNamesStop != null ? string.Join(", ", jobNamesStop) : "none")}");
                    break;
                case NetworkMessage.MessageTypes.Ping:
                    Debug.WriteLine("Client sent ping");
                    break;
                case NetworkMessage.MessageTypes.Pong:
                    Debug.WriteLine("Client sent pong");
                    break;
            }
        }

        private string GetLocalIPv4Address()
        {
            var result = string.Empty;
            try
            {
                // Get all network interfaces
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                
                // Find active interfaces that are not loopbacks
                foreach (NetworkInterface adapter in interfaces)
                {
                    if (adapter.OperationalStatus == OperationalStatus.Up &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (var unicastAddr in adapter.GetIPProperties().UnicastAddresses)
                        {
                            if (unicastAddr.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                result = unicastAddr.Address.ToString();
                                return result; // Return the first found IPv4 address
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting local IP: {ex.Message}");
            }
            
            // Fallback to loopback
            return "127.0.0.1";
        }

        public void Shutdown()
        {
            StopServer();
            _server = null;
        }
    }

    public class ClientConnectionInfo
    {
        public string ClientId { get; set; }
        public string IpAddress { get; set; }
        public DateTime ConnectedSince { get; set; }
    }
}