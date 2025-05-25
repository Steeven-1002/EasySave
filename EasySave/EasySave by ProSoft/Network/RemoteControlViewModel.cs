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

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand LaunchJobsCommand { get; }
        public ICommand PauseJobsCommand { get; }
        public ICommand ResumeJobsCommand { get; }
        public ICommand StopJobsCommand { get; }

        public RemoteControlViewModel()
        {
            ConnectCommand = new RelayCommand(_ => Connect(), _ => !IsConnected);
            DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsConnected);
            LaunchJobsCommand = new RelayCommand(_ => LaunchSelectedJobs(), _ => CanLaunchSelectedJobs());
            PauseJobsCommand = new RelayCommand(_ => PauseSelectedJobs(), _ => CanPauseSelectedJobs());
            ResumeJobsCommand = new RelayCommand(_ => ResumeSelectedJobs(), _ => CanResumeSelectedJobs());
            StopJobsCommand = new RelayCommand(_ => StopSelectedJobs(), _ => CanStopSelectedJobs());
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
            if (IsConnected) return;

            ConnectionStatus = "Connecting...";
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ServerAddress, ServerPort);

                IsConnected = true;
                ConnectionStatus = $"Connected to {ServerAddress}:{ServerPort}";

                _cancellationTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = $"Connection failed: {ex.Message}";
                _client?.Close();
                _client = null;
            }
        }

        public void Disconnect()
        {
            if (!IsConnected) return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _client?.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during disconnect: {ex.Message}");
            }
            finally
            {
                _client = null;
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                Jobs.Clear();
                IsBusinessSoftwareRunning = false;
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken token)
        {
            try
            {
                using NetworkStream stream = _client.GetStream();
                byte[] buffer = new byte[8192]; // Increased buffer size for potential large job lists

                while (!token.IsCancellationRequested && _client.Connected)
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
                        await Task.Delay(100, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when token is canceled
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error receiving messages: {ex.Message}");

                // Handle disconnection on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected = false;
                    ConnectionStatus = $"Connection lost: {ex.Message}";
                    _client = null;
                });
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
                var jobStates = JsonSerializer.Deserialize<List<JobState>>(jobStatesElement.GetRawText());
                if (jobStates == null) return;

                // Update UI on the UI thread
                App.Current.Dispatcher.Invoke(() =>
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

                    // Clear and rebuild job list
                    Jobs.Clear();
                    foreach (var state in jobStates)
                    {
                        var job = new RemoteJobStatus
                        {
                            Name = state.JobName,
                            SourcePath = state.SourcePath,
                            TargetPath = state.TargetPath,
                            Type = state.Type,
                            State = state.State,
                            TotalFiles = state.TotalFiles,
                            TotalSize = state.TotalSize,
                            RemainingFiles = state.RemainingFiles,
                            RemainingSize = state.RemainingSize,
                            CurrentSourceFile = state.CurrentSourceFile,
                            CurrentTargetFile = state.CurrentTargetFile,
                            StartTime = state.StartTime,
                            EndTime = state.EndTime,
                            IsSelected = selectedJobNames.Contains(state.JobName)
                        };

                        Jobs.Add(job);
                    }

                    CommandManager.InvalidateRequerySuggested();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing job statuses: {ex.Message}");
            }
        }

        private void ProcessBusinessAppState(bool isRunning)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsBusinessSoftwareRunning = isRunning;
            });
        }

        private async Task SendCommandAsync(string commandType, string jobName = "", List<string> jobNames = null, Dictionary<string, object> parameters = null)
        {
            if (!IsConnected) return;

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

                using NetworkStream stream = _client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                Debug.WriteLine($"Sent command: {commandType} for {jobNames?.Count ?? 0} jobs");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending command: {ex.Message}");
                ConnectionStatus = $"Error sending command: {ex.Message}";

                // Handle connection issues
                if (!_client.Connected)
                {
                    Disconnect();
                }
            }
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
                _ = SendCommandAsync("StopJobs", jobNames: jobNames);
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

        public int ProcessedFiles => TotalFiles - RemainingFiles;
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