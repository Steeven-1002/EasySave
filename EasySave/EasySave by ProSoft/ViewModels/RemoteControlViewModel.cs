using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network.Models;
using EasySave_by_ProSoft.Network.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// ViewModel for the Remote Control View, handling remote job monitoring and control.
    /// </summary>
    public class RemoteControlViewModel : INotifyPropertyChanged
    {
        private readonly INetworkConnectionService _networkService;

        private string _serverAddress = "127.0.0.1";
        private int _serverPort = 55555;
        private string _connectionStatus = "Disconnected";
        private string _statusMessage = string.Empty;
        private bool _isConnected;
        private bool _isOperationInProgress;
        private RemoteJob? _selectedJob;

        public ObservableCollection<RemoteJob> RemoteJobs { get; } = new();

        public string ServerAddress
        {
            get => _serverAddress;
            set { _serverAddress = value; OnPropertyChanged(); }
        }

        public int ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(); }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            set { _isOperationInProgress = value; OnPropertyChanged(); }
        }

        public RemoteJob? SelectedJob
        {
            get => _selectedJob;
            set { _selectedJob = value; OnPropertyChanged(); }
        }

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand RefreshJobsCommand { get; }
        public ICommand StartJobCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }

        public RemoteControlViewModel()
            : this(new TcpNetworkConnectionService())
        {
        }

        public RemoteControlViewModel(INetworkConnectionService networkService)
        {
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));

            ConnectCommand = new RelayCommand(async _ => await ConnectAsync(), _ => !IsConnected && !IsOperationInProgress);
            DisconnectCommand = new RelayCommand(async _ => await DisconnectAsync(), _ => IsConnected && !IsOperationInProgress);
            RefreshJobsCommand = new RelayCommand(async _ => await RefreshJobsAsync(), _ => IsConnected && !IsOperationInProgress);
            StartJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("Start"), _ => CanSendJobCommand());
            PauseJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("Pause"), _ => CanSendJobCommand());
            ResumeJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("Resume"), _ => CanSendJobCommand());
            StopJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("Stop"), _ => CanSendJobCommand());

            _networkService.ConnectionStatusChanged += (s, msg) => ConnectionStatus = msg;
            _networkService.StatusMessageChanged += (s, msg) => StatusMessage = msg;
            _networkService.JobStatusesUpdated += (s, jobs) => UpdateRemoteJobs(jobs);
        }

        private async Task ConnectAsync()
        {
            IsOperationInProgress = true;
            try
            {
                await _networkService.ConnectAsync(ServerAddress, ServerPort);
                IsConnected = _networkService.IsConnected;
                if (IsConnected)
                    await RefreshJobsAsync();
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private async Task DisconnectAsync()
        {
            IsOperationInProgress = true;
            try
            {
                await _networkService.DisconnectAsync();
                IsConnected = false;
                RemoteJobs.Clear();
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private async Task RefreshJobsAsync()
        {
            if (IsConnected)
            {
                await _networkService.RequestJobStatusUpdateAsync();
            }
        }

        private bool CanSendJobCommand()
        {
            return IsConnected && SelectedJob != null && !IsOperationInProgress;
        }

        private async Task SendJobCommandAsync(string commandType)
        {
            if (SelectedJob == null) return;
            IsOperationInProgress = true;
            try
            {
                var cmd = new RemoteCommand(commandType)
                {
                    JobNames = new System.Collections.Generic.List<string> { SelectedJob.Name }
                };
                await _networkService.SendCommandAsync(cmd);
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private void UpdateRemoteJobs(System.Collections.Generic.List<JobState> jobStates)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                RemoteJobs.Clear();
                foreach (var state in jobStates)
                {
                    RemoteJobs.Add(RemoteJob.FromJobState(state));
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        internal void Disconnect()
        {
            throw new NotImplementedException();
        }
    }
}