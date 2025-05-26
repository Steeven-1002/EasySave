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
    public class RemoteControlViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly INetworkConnectionService _networkService;

        private string _serverAddress = "127.0.0.1";
        private int _serverPort = 55555;
        private string _connectionStatus = "Disconnected";
        private string _statusMessage = string.Empty;
        private bool _isConnected;
        private bool _isOperationInProgress;
        private RemoteJob? _selectedJob;
        internal Action<JobStatus> FollowJob;
        private System.Threading.Timer? _connectionCheckTimer;

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
            StartJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("LaunchJobs"), _ => CanSendJobCommand());
            PauseJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("PauseJobs"), _ => CanSendJobCommand() && CanPauseJob());
            ResumeJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("ResumeJobs"), _ => CanSendJobCommand() && CanResumeJob());
            StopJobCommand = new RelayCommand(async _ => await SendJobCommandAsync("StopJobs"), _ => CanSendJobCommand() && CanStopJob());

            _networkService.ConnectionStatusChanged += (s, msg) =>
            {
                ConnectionStatus = msg;
                IsConnected = _networkService.IsConnected;
            };

            _networkService.StatusMessageChanged += (s, msg) => StatusMessage = msg;
            _networkService.JobStatusesUpdated += (s, jobs) => UpdateRemoteJobs(jobs);

            _connectionCheckTimer = new System.Threading.Timer(
                CheckConnectionStatus,
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10)
            );
        }

        private async Task ConnectAsync()
        {
            IsOperationInProgress = true;
            StatusMessage = "Connecting to server...";

            try
            {
                System.Diagnostics.Debug.WriteLine($"Connecting to {ServerAddress}:{ServerPort}");
                await _networkService.ConnectAsync(ServerAddress, ServerPort);

                IsConnected = _networkService.IsConnected;

                if (IsConnected)
                {
                    StatusMessage = "Connected successfully. Requesting job statuses...";
                    System.Diagnostics.Debug.WriteLine("Connection successful, refreshing jobs");
                    await RefreshJobsAsync();
                }
                else
                {
                    StatusMessage = "Connection failed";
                    System.Diagnostics.Debug.WriteLine("Connection failed (IsConnected is false)");
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusMessage = $"Connection error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Connection error: {ex}");
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
            if (!IsConnected)
            {
                StatusMessage = "Not connected to server";
                return;
            }

            if (IsOperationInProgress)
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Refresh jobs skipped - operation already in progress");
                return;
            }

            IsOperationInProgress = true;
            var operationStartTime = DateTime.Now;
            try
            {
                StatusMessage = "Requesting job status updates...";
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Starting RefreshJobsAsync");

                int maxRetries = 3;
                int currentRetry = 0;
                bool success = false;
                Exception? lastException = null;

                while (!success && currentRetry < maxRetries)
                {
                    try
                    {
                        if (currentRetry > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Retrying job status update (attempt {currentRetry + 1}/{maxRetries})");
                            StatusMessage = $"Retrying job status update ({currentRetry + 1}/{maxRetries})...";
                            await Task.Delay(500 * currentRetry);
                        }

                        if (!_networkService.IsConnected)
                        {
                            throw new InvalidOperationException("Connection to server lost");
                        }

                        await _networkService.RequestJobStatusUpdateAsync();
                        success = true;

                        if (currentRetry > 0)
                        {
                            StatusMessage = "Job status update successful after retry";
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        currentRetry++;

                        if (currentRetry >= maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Max retries reached");
                            break;
                        }

                        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Job status update attempt {currentRetry} failed: {ex.Message}");

                        if (!_networkService.IsConnected)
                        {
                            IsConnected = false;
                            break;
                        }
                    }
                }

                if (!success && lastException != null)
                {
                    throw lastException;
                }
                else if (success)
                {
                    // Update status message after a successful operation
                    if (RemoteJobs.Count > 0)
                    {
                        StatusMessage = $"Received status for {RemoteJobs.Count} jobs";
                    }
                    else
                    {
                        StatusMessage = "No jobs found on server";
                        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] No jobs found on server");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing jobs: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RefreshJobsAsync error: {ex}");

                if (!_networkService.IsConnected)
                {
                    IsConnected = false;
                    ConnectionStatus = "Connection lost";
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Connection marked as lost during refresh");
                    RemoteJobs.Clear();
                }
            }
            finally
            {
                // Safety check - if the operation has been in progress for too long, force reset
                if ((DateTime.Now - operationStartTime).TotalSeconds > 30)
                {
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Operation timeout detected, force resetting IsOperationInProgress flag");
                }
                
                IsOperationInProgress = false;
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Finished RefreshJobsAsync");
            }
        }

        private bool CanSendJobCommand()
        {
            return IsConnected && SelectedJob != null && !IsOperationInProgress;
        }

        private bool CanPauseJob()
        {
            return SelectedJob != null && SelectedJob.State == BackupState.Running;
        }

        private bool CanResumeJob()
        {
            return SelectedJob != null && SelectedJob.State == BackupState.Paused;
        }

        private bool CanStopJob()
        {
            return SelectedJob != null &&
                  (SelectedJob.State == BackupState.Running ||
                   SelectedJob.State == BackupState.Paused);
        }

        private async Task SendJobCommandAsync(string commandType)
        {
            if (SelectedJob == null)
            {
                StatusMessage = "No job selected";
                return;
            }

            IsOperationInProgress = true;
            try
            {
                if (!_networkService.IsConnected)
                {
                    throw new InvalidOperationException("Not connected to server");
                }

                StatusMessage = $"Sending {commandType} command...";
                var cmd = new Network.RemoteCommand(commandType)
                {
                    JobNames = new System.Collections.Generic.List<string> { SelectedJob.Name }
                };

                await _networkService.SendCommandAsync(cmd);
                StatusMessage = $"{commandType} command sent successfully";

                await Task.Delay(750);

                if (_networkService.IsConnected)
                {
                    StatusMessage = "Updating job status...";
                    await RefreshJobsAsync();
                }
                else
                {
                    StatusMessage = "Connection lost after sending command";
                    IsConnected = false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sending {commandType} command: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in SendJobCommandAsync: {ex}");

                if (!_networkService.IsConnected)
                {
                    IsConnected = false;
                    ConnectionStatus = "Connection lost";
                }
            }
            finally
            {
                IsOperationInProgress = false;
            }
        }

        private void UpdateRemoteJobs(System.Collections.Generic.List<JobState> jobStates)
        {
            if (jobStates == null || jobStates.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UpdateRemoteJobs called with empty or null job states");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UpdateRemoteJobs called with {jobStates.Count} job states");
            
            // Ensure UI updates on the main thread
            App.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    string? selectedJobName = SelectedJob?.Name;
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Current selected job: {selectedJobName ?? "none"}");

                    // Store old count for debugging
                    int oldCount = RemoteJobs.Count;
                    
                    // Clear and refresh the collection
                    RemoteJobs.Clear();
                    
                    foreach (var state in jobStates)
                    {
                        try
                        {
                            if (state != null && !string.IsNullOrEmpty(state.JobName))
                            {
                                var remoteJob = RemoteJob.FromJobState(state);
                                if (remoteJob != null)
                                {
                                    RemoteJobs.Add(remoteJob);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error adding job {state?.JobName}: {ex.Message}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RemoteJobs updated: {oldCount} -> {RemoteJobs.Count}");
                    
                    // If there are jobs in the list and nothing is selected, select the first one
                    if (RemoteJobs.Count > 0 && SelectedJob == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Auto-selecting first job: {RemoteJobs[0].Name}");
                        SelectedJob = RemoteJobs[0];
                    }
                    // Otherwise try to restore the previous selection
                    else if (!string.IsNullOrEmpty(selectedJobName))
                    {
                        RemoteJob? matchedJob = RemoteJobs.FirstOrDefault(j => j.Name == selectedJobName);
                        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Attempting to restore selection: {(matchedJob != null ? "found" : "not found")}");
                        SelectedJob = matchedJob;
                    }
                    
                    // Force property change notification for the collection
                    OnPropertyChanged(nameof(RemoteJobs));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error in UpdateRemoteJobs: {ex}");
                }
            });
        }

        private void CheckConnectionStatus(object? state)
        {
            try
            {
                bool serviceConnected = _networkService.IsConnected;
                if (IsConnected != serviceConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"Connection state mismatch detected: " +
                        $"ViewModel={IsConnected}, Service={serviceConnected}");

                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        IsConnected = serviceConnected;

                        if (!IsConnected)
                        {
                            RemoteJobs.Clear();
                            ConnectionStatus = "Connection lost";
                            StatusMessage = "Connection to server was lost";
                        }
                    });
                }

                if (IsConnected && !IsOperationInProgress && DateTime.Now.Second % 30 == 0)
                {
                    App.Current?.Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("Auto-refreshing job statuses");
                            await RefreshJobsAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Auto-refresh error: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in connection check timer: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        internal void Disconnect()
        {
            _ = DisconnectAsync();
        }

        public void Dispose()
        {
            _connectionCheckTimer?.Dispose();
            _connectionCheckTimer = null;
        }
    }
}