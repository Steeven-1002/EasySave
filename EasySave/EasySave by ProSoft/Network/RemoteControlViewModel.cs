using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;

namespace EasySave_by_ProSoft.Network
{
    /// <summary>
    /// ViewModel for remote control client functionality
    /// </summary>
    public class RemoteControlViewModel : INotifyPropertyChanged
    {
        private TcpClient _client;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;
        private string _serverAddress = "127.0.0.1";
        private int _serverPort = 55555;
        private string _connectionStatus = "Disconnected";
        private bool _isBusinessSoftwareRunning = false;
        private ObservableCollection<RemoteJobStatus> _jobs = new();
        private DispatcherTimer _autoRefreshTimer;
        private bool _autoRefreshEnabled = true;
        private double _refreshInterval = 3.0; // Default refresh interval in seconds
        private bool _isRealTimeFollowEnabled = true;
        private string _lastOperationStatus = "";
        private bool _isOperationInProgress = false;

        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                if (_serverAddress != value)
                {
                    _serverAddress = value;
                    OnPropertyChanged();
                }
            }
        }

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

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();

                    // Start/stop the auto-refresh timer based on connection state
                    if (_isConnected && AutoRefreshEnabled)
                        StartAutoRefresh();
                    else
                        StopAutoRefresh();
                }
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBusinessSoftwareRunning
        {
            get => _isBusinessSoftwareRunning;
            private set
            {
                if (_isBusinessSoftwareRunning != value)
                {
                    _isBusinessSoftwareRunning = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<RemoteJobStatus> Jobs
        {
            get => _jobs;
            private set
            {
                if (_jobs != value)
                {
                    _jobs = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoRefreshEnabled
        {
            get => _autoRefreshEnabled;
            set
            {
                if (_autoRefreshEnabled != value)
                {
                    _autoRefreshEnabled = value;
                    OnPropertyChanged();

                    if (_isConnected)
                    {
                        if (_autoRefreshEnabled)
                            StartAutoRefresh();
                        else
                            StopAutoRefresh();
                    }
                }
            }
        }

        public double RefreshInterval
        {
            get => _refreshInterval;
            set
            {
                if (_refreshInterval != value && value >= 1.0)
                {
                    _refreshInterval = value;
                    OnPropertyChanged();

                    // Update the timer interval if it's running
                    if (_autoRefreshTimer != null && _autoRefreshTimer.IsEnabled)
                    {
                        _autoRefreshTimer.Interval = TimeSpan.FromSeconds(_refreshInterval);
                    }
                }
            }
        }

        public bool IsRealTimeFollowEnabled
        {
            get => _isRealTimeFollowEnabled;
            set
            {
                if (_isRealTimeFollowEnabled != value)
                {
                    _isRealTimeFollowEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LastOperationStatus
        {
            get => _lastOperationStatus;
            set
            {
                if (_lastOperationStatus != value)
                {
                    _lastOperationStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            set
            {
                if (_isOperationInProgress != value)
                {
                    _isOperationInProgress = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand LaunchJobsCommand { get; }
        public ICommand PauseJobsCommand { get; }
        public ICommand ResumeJobsCommand { get; }
        public ICommand StopJobsCommand { get; }
        public ICommand RefreshJobsCommand { get; }

        public RemoteControlViewModel()
        {
            ConnectCommand = new RelayCommand(_ => Connect(), _ => !IsConnected && !IsOperationInProgress);
            DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected && !IsOperationInProgress);
            LaunchJobsCommand = new RelayCommand(_ => LaunchSelectedJobs(), _ => CanLaunchSelectedJobs() && !IsOperationInProgress);
            PauseJobsCommand = new RelayCommand(_ => PauseSelectedJobs(), _ => CanPauseSelectedJobs() && !IsOperationInProgress);
            ResumeJobsCommand = new RelayCommand(_ => ResumeSelectedJobs(), _ => CanResumeSelectedJobs() && !IsOperationInProgress);
            StopJobsCommand = new RelayCommand(_ => StopSelectedJobs(), _ => CanStopSelectedJobs() && !IsOperationInProgress);
            RefreshJobsCommand = new RelayCommand(_ => RefreshJobs(), _ => IsConnected && !IsOperationInProgress);

            // Initialize the auto-refresh timer
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_refreshInterval)
            };
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
        }

        private void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            // Ensure we don't try to refresh if we're not connected or if an operation is in progress
            if (!IsConnected || IsOperationInProgress || _client == null || !_client.Connected)
            {
                // If we thought we were connected but the socket is actually disconnected
                if (IsConnected && (_client == null || !_client.Connected))
                {
                    Debug.WriteLine("AutoRefreshTimer_Tick detected disconnected socket");
                    HandleConnectionLoss();
                }
                return;
            }

            RequestJobStatusUpdate();
        }

        private void StartAutoRefresh()
        {
            if (_autoRefreshTimer != null && !_autoRefreshTimer.IsEnabled && IsConnected && _client != null && _client.Connected)
            {
                _autoRefreshTimer.Start();
                Debug.WriteLine($"Auto-refresh started with interval: {_refreshInterval} seconds");
            }
        }

        private void StopAutoRefresh()
        {
            if (_autoRefreshTimer != null && _autoRefreshTimer.IsEnabled)
            {
                _autoRefreshTimer.Stop();
                Debug.WriteLine("Auto-refresh stopped");
            }
        }

        private void RefreshJobs()
        {
            if (IsConnected)
            {
                RequestJobStatusUpdate();
                LastOperationStatus = "Manual refresh completed";
            }
        }

        private async void RequestJobStatusUpdate()
        {
            // Check connection state before trying to request updates
            if (!IsConnected || _client == null || !_client.Connected)
            {
                if (IsConnected)
                {
                    Debug.WriteLine("RequestJobStatusUpdate detected socket is disconnected while IsConnected=true");
                    HandleConnectionLoss();
                }
                return;
            }

            try
            {
                await SendCommandAsync("RequestStatusUpdate");
                Debug.WriteLine("Job status update requested");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error requesting job status update: {ex.Message}");
                LastOperationStatus = $"Error requesting update: {ex.Message}";
            }
        }

        private bool CanLaunchSelectedJobs()
        {
            if (!IsConnected || IsBusinessSoftwareRunning) return false;

            foreach (var job in Jobs)
            {
                if (job.IsSelected && (job.State == BackupState.Initialise || job.State == BackupState.Error || job.State == BackupState.Completed))
                {
                    return true;
                }
            }
            return false;
        }

        private bool CanPauseSelectedJobs()
        {
            if (!IsConnected) return false;

            foreach (var job in Jobs)
            {
                if (job.IsSelected && job.State == BackupState.Running)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CanResumeSelectedJobs()
        {
            if (!IsConnected || IsBusinessSoftwareRunning) return false;

            foreach (var job in Jobs)
            {
                if (job.IsSelected && job.State == BackupState.Paused)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CanStopSelectedJobs()
        {
            if (!IsConnected) return false;

            foreach (var job in Jobs)
            {
                if (job.IsSelected && (job.State == BackupState.Running || job.State == BackupState.Paused))
                {
                    return true;
                }
            }
            return false;
        }

        public async Task Connect()
        {
            if (IsConnected || IsOperationInProgress) return;

            IsOperationInProgress = true;
            ConnectionStatus = "Connecting...";
            LastOperationStatus = "Connecting to server...";

            try
            {
                // Close any existing client first
                if (_client != null)
                {
                    _client.Close();
                    _client.Dispose();
                    _client = null;
                }

                _client = new TcpClient();
                var connectTask = _client.ConnectAsync(ServerAddress, ServerPort);
                
                // Add a timeout to the connection attempt
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                {
                    // Connection timed out
                    throw new TimeoutException("Connection attempt timed out");
                }

                // Ensure the connection task completed successfully
                await connectTask;

                // Additional check to verify connection
                if (!_client.Connected)
                {
                    throw new InvalidOperationException("Failed to establish connection");
                }

                IsConnected = true;
                ConnectionStatus = $"Connected to {ServerAddress}:{ServerPort}";
                LastOperationStatus = "Connected successfully";

                _cancellationTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token));
            }
            catch (SocketException ex)
            {
                IsConnected = false;
                ConnectionStatus = $"Connection failed: {ex.Message}";
                LastOperationStatus = $"Connection failed: {ex.Message}";
                _client?.Close();
                _client = null;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = $"Connection failed: {ex.Message}";
                LastOperationStatus = $"Connection failed: {ex.Message}";
                _client?.Close();
                _client = null;
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        public void Disconnect()
        {
            if (!IsConnected || IsOperationInProgress) return;

            IsOperationInProgress = true;
            try
            {
                // Stop auto-refresh first
                StopAutoRefresh();
                
                // Cancel the token before closing the client
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                if (_client != null)
                {
                    if (_client.Connected)
                    {
                        try
                        {
                            // Try to gracefully close the connection if possible
                            NetworkStream stream = _client.GetStream();
                            stream.Close(2000); // Give it 2 seconds to flush and close properly
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error closing stream gracefully: {ex.Message}");
                        }

                        _client.Close();
                    }
                    _client.Dispose();
                    _client = null;
                }

                LastOperationStatus = "Disconnected from server";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during disconnect: {ex.Message}");
                LastOperationStatus = $"Error during disconnect: {ex.Message}";
            }
            finally
            {
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                Jobs.Clear();
                IsBusinessSoftwareRunning = false;
                IsOperationInProgress = false;
                _client = null;
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken token)
        {
            try
            {
                // Validate client before accessing the stream
                if (_client == null || !_client.Connected)
                {
                    throw new InvalidOperationException("Cannot receive messages from a disconnected socket");
                }

                using NetworkStream stream = _client.GetStream();
                byte[] buffer = new byte[8192]; // Increased buffer size for potential large job lists

                while (!token.IsCancellationRequested && _client != null && _client.Connected)
                {
                    try
                    {
                        if (stream.DataAvailable)
                        {
                            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                            if (bytesRead > 0)
                            {
                                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                ProcessServerMessage(json);
                            }
                        }
                        else
                        {
                            // Use a shorter delay and check connection more frequently
                            await Task.Delay(100, token);
                            
                            // Periodically check if the client is still connected
                            if (_client == null || !_client.Connected)
                            {
                                throw new SocketException((int)SocketError.Disconnecting);
                            }
                        }
                    }
                    catch (System.IO.IOException ex)
                    {
                        // Socket was closed, log and rethrow
                        Debug.WriteLine($"Socket IO Exception: {ex.Message}");
                        throw;
                    }
                    catch (SocketException ex)
                    {
                        Debug.WriteLine($"Socket Exception during receive: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when token is canceled
                Debug.WriteLine("Receive messages operation was canceled");
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine("Network stream or client was disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error receiving messages: {ex.Message}");

                // Handle disconnection on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected = false;
                    ConnectionStatus = $"Connection lost: {ex.Message}";
                    LastOperationStatus = $"Connection lost: {ex.Message}";
                    _client = null;
                    StopAutoRefresh();
                });
            }
        }

        private async Task SendCommandAsync(string commandType, string jobName = "", List<string> jobNames = null, Dictionary<string, object> parameters = null)
        {
            // Early validation to prevent operations on non-connected sockets
            if (!IsConnected || _client == null || !_client.Connected)
            {
                // If we previously thought we were connected but the socket is actually disconnected
                if (IsConnected)
                {
                    Debug.WriteLine("SendCommandAsync detected socket is disconnected while IsConnected=true");
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        IsConnected = false;
                        ConnectionStatus = "Connection lost";
                        LastOperationStatus = "Connection lost - cannot send command";
                        _client = null;
                    });
                    StopAutoRefresh();
                }
                return;
            }

            IsOperationInProgress = true;
            try
            {
                var command = new RemoteCommand
                {
                    CommandType = commandType,
                    JobName = jobName,
                    JobNames = jobNames ?? new List<string>(),
                    Parameters = parameters ?? new Dictionary<string, object>()
                };

                string json = JsonSerializer.Serialize(command);
                byte[] data = Encoding.UTF8.GetBytes(json);

                // Additional check right before accessing the stream
                if (_client == null || !_client.Connected)
                {
                    throw new InvalidOperationException("Socket disconnected before sending command");
                }

                using NetworkStream stream = _client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                Debug.WriteLine($"Sent command: {commandType} for {jobNames?.Count ?? 0} jobs");

                if (commandType != "RequestStatusUpdate")
                {
                    LastOperationStatus = $"{commandType} command sent to server";
                }
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"Socket already disposed: {ex.Message}");
                ConnectionStatus = "Connection closed";
                LastOperationStatus = "Connection closed - cannot send command";
                HandleConnectionLoss();
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Invalid socket operation: {ex.Message}");
                ConnectionStatus = "Connection invalid";
                LastOperationStatus = "Connection invalid - command not sent";
                HandleConnectionLoss();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"Socket error: {ex.Message}");
                ConnectionStatus = $"Socket error: {ex.Message}";
                LastOperationStatus = $"Socket error - command not sent";
                HandleConnectionLoss();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending command: {ex.Message}");
                ConnectionStatus = $"Error sending command: {ex.Message}";
                LastOperationStatus = $"Error sending command: {ex.Message}";

                // Handle connection issues
                if (_client == null || !_client.Connected)
                {
                    HandleConnectionLoss();
                }
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private void HandleConnectionLoss()
        {
            // Safely handle connection loss
            App.Current.Dispatcher.Invoke(() =>
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    StopAutoRefresh();
                    _client = null;
                    Jobs.Clear();
                    IsBusinessSoftwareRunning = false;
                }
            });
        }

        public void CheckConnectionStatus()
        {
            if (!IsConnected || _client == null)
            {
                return;
            }

            // Check if the client is still connected
            if (!_client.Connected)
            {
                Debug.WriteLine("Connection check detected socket is disconnected while IsConnected=true");
                HandleConnectionLoss();
            }
            else
            {
                // Request a status update to validate the connection is working
                RequestJobStatusUpdate();
            }
        }

        private void ProcessServerMessage(string json)
        {
            try
            {
                var message = JsonDocument.Parse(json);
                string messageType = message.RootElement.GetProperty("Type").GetString();

                switch (messageType)
                {
                    case "JobStatuses":
                        ProcessJobStatuses(message.RootElement.GetProperty("Data"));
                        break;

                    case "BusinessAppState":
                        ProcessBusinessAppState(message.RootElement.GetProperty("IsRunning").GetBoolean());
                        break;

                    case "CommandResponse":
                        if (message.RootElement.TryGetProperty("Status", out JsonElement statusElement))
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                LastOperationStatus = statusElement.GetString();
                            });
                        }
                        break;

                    default:
                        Debug.WriteLine($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private void ProcessJobStatuses(JsonElement jobStatesElement)
        {
            try
            {
                List<JobState> jobStates;
                try
                {
                    jobStates = JsonSerializer.Deserialize<List<JobState>>(jobStatesElement.GetRawText());
                    if (jobStates == null) return;
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"Error deserializing job states: {ex.Message}");
                    return;
                }

                // Update UI on the UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Remember selected state of existing jobs
                        var selectedJobNames = new HashSet<string>();
                        foreach (var job in Jobs)
                        {
                            if (job.IsSelected)
                            {
                                selectedJobNames.Add(job.Name);
                            }
                        }

                        // Keep scrolling position if real-time follow is enabled
                        RemoteJobStatus jobToFollow = null;
                        if (IsRealTimeFollowEnabled && Jobs.Any(j => j.State == BackupState.Running))
                        {
                            jobToFollow = Jobs.FirstOrDefault(j => j.State == BackupState.Running);
                        }

                        // Clear and rebuild job list
                        Jobs.Clear();
                        foreach (var state in jobStates)
                        {
                            // Make sure all fields are valid before adding to the job list
                            if (state != null && !string.IsNullOrEmpty(state.JobName))
                            {
                                var job = new RemoteJobStatus
                                {
                                    Name = state.JobName,
                                    SourcePath = state.SourcePath ?? string.Empty,
                                    TargetPath = state.TargetPath ?? string.Empty,
                                    Type = state.Type,
                                    State = state.State,
                                    TotalFiles = Math.Max(0, state.TotalFiles), // Ensure positive values
                                    TotalSize = Math.Max(0, state.TotalSize),
                                    RemainingFiles = Math.Max(0, state.RemainingFiles),
                                    RemainingSize = Math.Max(0, state.RemainingSize),
                                    CurrentSourceFile = state.CurrentSourceFile ?? string.Empty,
                                    CurrentTargetFile = state.CurrentTargetFile ?? string.Empty,
                                    StartTime = state.StartTime,
                                    EndTime = state.EndTime,
                                    IsSelected = selectedJobNames.Contains(state.JobName)
                                };

                                Jobs.Add(job);
                            }
                        }

                        // Signal real-time follow if job is still running
                        if (IsRealTimeFollowEnabled && jobToFollow != null)
                        {
                            var updatedJob = Jobs.FirstOrDefault(j => j.Name == jobToFollow.Name && j.State == BackupState.Running);
                            if (updatedJob != null)
                            {
                                // Raise event to scroll to this job (UI will handle this)
                                RaiseFollowJobEvent(updatedJob);
                            }
                        }

                        CommandManager.InvalidateRequerySuggested();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating UI with job statuses: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing job statuses: {ex.Message}");
            }
        }

        // Event for notifying the UI to follow a specific job
        public event Action<RemoteJobStatus> FollowJob;

        private void RaiseFollowJobEvent(RemoteJobStatus job)
        {
            FollowJob?.Invoke(job);
        }

        private void ProcessBusinessAppState(bool isRunning)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsBusinessSoftwareRunning = isRunning;
            });
        }

        private void LaunchSelectedJobs()
        {
            var jobNames = new List<string>();
            foreach (var job in Jobs)
            {
                if (job.IsSelected && (job.State == BackupState.Initialise || job.State == BackupState.Error || job.State == BackupState.Completed))
                {
                    jobNames.Add(job.Name);
                }
            }

            if (jobNames.Count > 0)
            {
                LastOperationStatus = $"Starting {jobNames.Count} job(s)...";
                _ = SendCommandAsync("LaunchJobs", jobNames: jobNames);
            }
        }

        private void PauseSelectedJobs()
        {
            var jobNames = new List<string>();
            foreach (var job in Jobs)
            {
                if (job.IsSelected && job.State == BackupState.Running)
                {
                    jobNames.Add(job.Name);
                }
            }

            if (jobNames.Count > 0)
            {
                LastOperationStatus = $"Pausing {jobNames.Count} job(s)...";
                _ = SendCommandAsync("PauseJobs", jobNames: jobNames);
            }
        }

        private void ResumeSelectedJobs()
        {
            var jobNames = new List<string>();
            foreach (var job in Jobs)
            {
                if (job.IsSelected && job.State == BackupState.Paused)
                {
                    jobNames.Add(job.Name);
                }
            }

            if (jobNames.Count > 0)
            {
                LastOperationStatus = $"Resuming {jobNames.Count} job(s)...";
                _ = SendCommandAsync("ResumeJobs", jobNames: jobNames);
            }
        }

        private void StopSelectedJobs()
        {
            var jobNames = new List<string>();
            foreach (var job in Jobs)
            {
                if (job.IsSelected && (job.State == BackupState.Running || job.State == BackupState.Paused))
                {
                    jobNames.Add(job.Name);
                }
            }

            if (jobNames.Count > 0)
            {
                LastOperationStatus = $"Stopping {jobNames.Count} job(s)...";
                _ = SendCommandAsync("StopJobs", jobNames: jobNames);
            }
        }

        private bool IsTcpClientConnected()
        {
            try
            {
                if (_client == null)
                    return false;

                // First check the Connected property
                if (!_client.Connected)
                    return false;

                // If we're "connected" but Client/Socket is null, then we're not actually connected
                if (_client.Client == null)
                    return false;

                // Check if the socket is truly connected with a non-blocking poll
                // Poll with SelectRead - true means socket is closed or has pending data
                bool blockingState = _client.Client.Blocking;
                try
                {
                    _client.Client.Blocking = false;
                    
                    // Try to read 1 byte with timeout of 1 microsecond - if it throws or returns true, socket has issues
                    byte[] tmp = new byte[1];
                    if (_client.Client.Poll(1, SelectMode.SelectRead) && 
                        _client.Client.Receive(tmp, SocketFlags.Peek) == 0)
                    {
                        // If we can read 0 bytes, the connection is closed or broken
                        return false;
                    }
                    
                    // Everything seems fine
                    return true;
                }
                catch (SocketException)
                {
                    // Any socket exception means we're not connected properly
                    return false;
                }
                finally
                {
                    // Restore original blocking state
                    if (_client?.Client != null)
                        _client.Client.Blocking = blockingState;
                }
            }
            catch
            {
                return false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RemoteJobStatus : INotifyPropertyChanged
    {
        private string _name;
        private string _sourcePath;
        private string _targetPath;
        private BackupType _type;
        private BackupState _state;
        private int _totalFiles;
        private long _totalSize;
        private int _remainingFiles;
        private long _remainingSize;
        private string _currentSourceFile;
        private string _currentTargetFile;
        private DateTime _startTime;
        private DateTime? _endTime;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { if (_sourcePath != value) { _sourcePath = value; OnPropertyChanged(); } }
        }

        public string TargetPath
        {
            get => _targetPath;
            set { if (_targetPath != value) { _targetPath = value; OnPropertyChanged(); } }
        }

        public BackupType Type
        {
            get => _type;
            set { if (_type != value) { _type = value; OnPropertyChanged(); } }
        }

        public BackupState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(EstimatedTimeRemaining));
                }
            }
        }

        public int TotalFiles
        {
            get => _totalFiles;
            set
            {
                if (_totalFiles != value)
                {
                    _totalFiles = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProcessedFiles));
                }
            }
        }

        public long TotalSize
        {
            get => _totalSize;
            set
            {
                if (_totalSize != value)
                {
                    _totalSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(FormattedTotalSize));
                }
            }
        }

        public int RemainingFiles
        {
            get => _remainingFiles;
            set
            {
                if (_remainingFiles != value)
                {
                    _remainingFiles = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProcessedFiles));
                }
            }
        }

        public long RemainingSize
        {
            get => _remainingSize;
            set
            {
                if (_remainingSize != value)
                {
                    _remainingSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(TransferredSize));
                }
            }
        }

        public string CurrentSourceFile
        {
            get => _currentSourceFile;
            set { if (_currentSourceFile != value) { _currentSourceFile = value; OnPropertyChanged(); } }
        }

        public string CurrentTargetFile
        {
            get => _currentTargetFile;
            set { if (_currentTargetFile != value) { _currentTargetFile = value; OnPropertyChanged(); } }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ElapsedTime));
                }
            }
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ElapsedTime));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public int ProcessedFiles
        {
            get
            {
                // Calculate safely to avoid negative values
                return Math.Max(0, TotalFiles - RemainingFiles);
            }
        }

        public long TransferredSize => TotalSize - RemainingSize;

        public double ProgressPercentage
        {
            get
            {
                if (TotalSize <= 0) return (State == BackupState.Completed && TotalFiles == 0) ? 100 : 0;
                if (State == BackupState.Completed) return 100;
                return Math.Round((double)TransferredSize / TotalSize * 100, 2);
            }
            set
            {
                if (value < 0) value = 0;
                if (value > 100) value = 100;
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        public string FormattedTotalSize
        {
            get
            {
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                if (TotalSize >= GB)
                    return $"{TotalSize / (double)GB:F2} GB";
                else if (TotalSize >= MB)
                    return $"{TotalSize / (double)MB:F2} MB";
                else if (TotalSize >= KB)
                    return $"{TotalSize / (double)KB:F2} KB";
                else
                    return $"{TotalSize} B";
            }
        }

        public TimeSpan ElapsedTime => (EndTime ?? DateTime.Now) - StartTime;

        public TimeSpan EstimatedTimeRemaining
        {
            get
            {
                if (State != BackupState.Running || ElapsedTime.TotalSeconds < 1 || TransferredSize <= 0)
                    return TimeSpan.Zero;

                double bytesPerSecond = TransferredSize / ElapsedTime.TotalSeconds;
                if (bytesPerSecond <= 0) return TimeSpan.Zero;

                return TimeSpan.FromSeconds(RemainingSize / bytesPerSecond);
            }
        }

        public string StatusDisplay
        {
            get
            {
                return State switch
                {
                    BackupState.Waiting => "En attente",
                    BackupState.Running => "En cours",
                    BackupState.Paused => "En pause",
                    BackupState.Completed => "Terminé",
                    BackupState.Error => "Erreur",
                    _ => "Inconnu"
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}