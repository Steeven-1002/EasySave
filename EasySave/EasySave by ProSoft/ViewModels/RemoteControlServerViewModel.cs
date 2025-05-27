using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EasySave_by_ProSoft.ViewModels
{
    public class RemoteControlServerViewModel : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        private SocketServer _server;
        private int _serverPort = 9000;
        private string _serverStatus = "Server not running";
        private bool _isServerRunning = false;
        private ObservableCollection<ClientConnectionInfo> _connectedClients;
        private string _localIpAddress;

        public int ServerPort
        {
            get => _serverPort;
            set
            {
                if (_serverPort != value)
                {
                    _serverPort = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ServerStatus
        {
            get => _serverStatus;
            set
            {
                if (_serverStatus != value)
                {
                    _serverStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public System.Windows.Media.Brush ServerStatusBackground
        {
            get
            {
                return IsServerRunning
                    ? new SolidColorBrush(Colors.Green)
                    : new SolidColorBrush(Colors.DarkRed);
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
                    OnPropertyChanged(nameof(ServerButtonText));
                    OnPropertyChanged(nameof(ServerButtonColor));
                    OnPropertyChanged(nameof(ServerStatusBackground));
                }
            }
        }

        public string ServerButtonText
        {
            get => IsServerRunning ? "Stop Server" : "Start Server";
        }

        public System.Windows.Media.Brush ServerButtonColor
        {
            get
            {
                return IsServerRunning
                    ? new SolidColorBrush(Colors.Red)
                    : new SolidColorBrush(Colors.ForestGreen);
            }
        }

        public ObservableCollection<ClientConnectionInfo> ConnectedClients
        {
            get => _connectedClients;
            set
            {
                _connectedClients = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NoClientsVisibility));
            }
        }

        public Visibility NoClientsVisibility => ConnectedClients?.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

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
            set
            {
                if (_localIpAddress != value)
                {
                    _localIpAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ToggleServerCommand { get; }

        public RemoteControlServerViewModel(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _connectedClients = new ObservableCollection<ClientConnectionInfo>();
            
            // Initialize the IP address
            _localIpAddress = GetLocalIPv4Address();
            if (string.IsNullOrEmpty(_localIpAddress))
            {
                _localIpAddress = "Not available";
            }

            ToggleServerCommand = new RelayCommand(_ => ToggleServer());

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

        private void ToggleServer()
        {
            if (IsServerRunning)
            {
                StopServer();
            }
            else
            {
                StartServer();
            }
        }

        private void StartServer()
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
                    ServerStatus = $"Server running on port {ServerPort}";
                }
                else
                {
                    ServerStatus = $"Failed to start server on port {ServerPort}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting server: {ex.Message}");
                ServerStatus = $"Error starting server: {ex.Message}";
            }
        }

        private void StopServer()
        {
            try
            {
                _server?.Stop();
                IsServerRunning = false;
                ServerStatus = "Server stopped";
                ConnectedClients.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping server: {ex.Message}");
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
                    ConnectedClients.Add(newClient);
                });
            }
            else if (status.StartsWith("Client disconnected:"))
            {
                var clientId = status.Replace("Client disconnected:", "").Trim();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Remove client from list (in real implementation, match by ID)
                    if (ConnectedClients.Count > 0)
                        ConnectedClients.RemoveAt(ConnectedClients.Count - 1);
                });
            }
        }

        private void Server_MessageReceived(object sender, NetworkMessage message)
        {
            // Handle client messages (optional based on requirements)
            Debug.WriteLine($"Message received: {message.Type}");
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

        public void Cleanup()
        {
            StopServer();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ClientConnectionInfo
    {
        public string ClientId { get; set; }
        public string IpAddress { get; set; }
        public DateTime ConnectedSince { get; set; }
    }
}