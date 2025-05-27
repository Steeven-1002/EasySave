using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network;
using EasySave_by_ProSoft.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// ViewModel for the Remote Control Client view
    /// </summary>
    public class RemoteControlViewModel : INotifyPropertyChanged, IEventListener, IDisposable
    {
        // Implement INotifyPropertyChanged interface
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private NetworkClient _client;
        private string _serverHost = "localhost";
        private int _serverPort = 9000;
        private string _connectionStatus = "Not connected";
        private bool _isConnected = false;
        private ObservableCollection<RemoteJobViewModel> _remoteJobs;
        private Core.EventManager _eventManager = Core.EventManager.Instance;
        private bool _disposed = false;

        // Properties
        public string ServerHost
        {
            get => _serverHost;
            set
            {
                if (_serverHost != value)
                {
                    _serverHost = value;
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

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ConnectionStatusBackground));
                }
            }
        }

        public System.Windows.Media.Brush ConnectionStatusBackground
        {
            get
            {
                return IsConnected 
                    ? new SolidColorBrush(Colors.Green) 
                    : new SolidColorBrush(Colors.DarkRed);
            }
        }

        public string ConnectionButtonText
        {
            get => IsConnected ? "Disconnect" : "Connect";
        }

        public System.Windows.Media.Brush ConnectionButtonColor
        {
            get
            {
                return IsConnected
                    ? new SolidColorBrush(Colors.Red)
                    : new SolidColorBrush(Colors.ForestGreen);
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
                    OnPropertyChanged(nameof(ConnectionButtonText));
                    OnPropertyChanged(nameof(ConnectionButtonColor));
                    OnPropertyChanged(nameof(ConnectionStatusBackground));
                }
            }
        }

        public ObservableCollection<RemoteJobViewModel> RemoteJobs
        {
            get => _remoteJobs;
            set
            {
                _remoteJobs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NoJobsVisibility));
            }
        }

        public Visibility NoJobsVisibility => RemoteJobs?.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        // Commands
        public ICommand ToggleConnectionCommand { get; }
        public ICommand RefreshJobsCommand { get; }
        public ICommand StartJobCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }
        public ICommand StartSelectedJobsCommand { get; }
        public ICommand PauseSelectedJobsCommand { get; }
        public ICommand ResumeSelectedJobsCommand { get; }
        public ICommand StopSelectedJobsCommand { get; }

        public RemoteControlViewModel()
        {
            // Initialize collections
            _remoteJobs = new ObservableCollection<RemoteJobViewModel>();

            // Register with EventManager to receive job status updates
            _eventManager.AddListener(this);

            // Initialize commands
            ToggleConnectionCommand = new RelayCommand(_ => ToggleConnection());
            RefreshJobsCommand = new RelayCommand(_ => RefreshJobs(), _ => IsConnected);
            StartJobCommand = new RelayCommand(job => StartJob(job as RemoteJobViewModel), _ => IsConnected);
            PauseJobCommand = new RelayCommand(job => PauseJob(job as RemoteJobViewModel), _ => IsConnected);
            ResumeJobCommand = new RelayCommand(job => ResumeJob(job as RemoteJobViewModel), _ => IsConnected);
            StopJobCommand = new RelayCommand(job => StopJob(job as RemoteJobViewModel), _ => IsConnected);
            StartSelectedJobsCommand = new RelayCommand(_ => StartSelectedJobs(), _ => IsConnected && HasSelectedJobs());
            PauseSelectedJobsCommand = new RelayCommand(_ => PauseSelectedJobs(), _ => IsConnected && HasSelectedJobs());
            ResumeSelectedJobsCommand = new RelayCommand(_ => ResumeSelectedJobs(), _ => IsConnected && HasSelectedJobs());
            StopSelectedJobsCommand = new RelayCommand(_ => StopSelectedJobs(), _ => IsConnected && HasSelectedJobs());
        }

        private async void ToggleConnection()
        {
            if (IsConnected)
            {
                Disconnect();
            }
            else
            {
                await ConnectAsync();
            }
        }

        private async Task ConnectAsync()
        {
            try
            {
                ConnectionStatus = "Connecting...";

                // Clean up previous client if needed
                if (_client != null)
                {
                    _client.ConnectionStatusChanged -= Client_ConnectionStatusChanged;
                    _client.JobStatusesReceived -= Client_JobStatusesReceived;
                    _client.ErrorOccurred -= Client_ErrorOccurred;
                }
                
                // Default to localhost if no server is specified
                var host = string.IsNullOrWhiteSpace(ServerHost) ? "localhost" : ServerHost;
                
                _client = new NetworkClient(host, ServerPort);
                _client.ConnectionStatusChanged += Client_ConnectionStatusChanged;
                _client.JobStatusesReceived += Client_JobStatusesReceived;
                _client.ErrorOccurred += Client_ErrorOccurred;

                // Connect to server
                bool connected = await _client.ConnectAsync();

                if (connected)
                {
                    IsConnected = true;
                    ConnectionStatus = $"Connected to {host}:{ServerPort}";
                    RefreshJobs();
                }
                else
                {
                    ConnectionStatus = $"Failed to connect to {host}:{ServerPort}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection error: {ex.Message}");
                ConnectionStatus = $"Connection error: {ex.Message}";
            }
        }

        private void Disconnect()
        {
            try
            {
                _client?.Disconnect();
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                RemoteJobs.Clear();
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Disconnect error: {ex.Message}");
                ConnectionStatus = $"Disconnect error: {ex.Message}";
            }
        }

        private async void RefreshJobs()
        {
            if (!IsConnected || _client == null)
                return;

            try
            {
                ConnectionStatus = "Refreshing job list...";
                await _client.RequestJobStatusesAsync();
                ConnectionStatus = $"Connected to {ServerHost}:{ServerPort}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing jobs: {ex.Message}");
                ConnectionStatus = $"Error refreshing: {ex.Message}";
                
                // Don't attempt to reconnect immediately as it might cause a loop
                // Instead, let the automatic reconnect handle it if needed
            }
        }

        private void UpdateJobList(List<JobState> jobStates)
        {
            try
            {
                // Create a dictionary of existing jobs for fast lookups
                var existingJobs = RemoteJobs.ToDictionary(job => job.JobName, job => job);
                var updatedJobs = new ObservableCollection<RemoteJobViewModel>();

                foreach (var state in jobStates)
                {
                    if (existingJobs.TryGetValue(state.JobName, out var existingJob))
                    {
                        // Update existing job
                        existingJob.Update(state);
                        updatedJobs.Add(existingJob);
                    }
                    else
                    {
                        // Create new job
                        var newJob = new RemoteJobViewModel(state);
                        updatedJobs.Add(newJob);
                    }
                }

                // Update the collection
                RemoteJobs = updatedJobs;
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating job list: {ex.Message}");
            }
        }

        private async void StartJob(RemoteJobViewModel job)
        {
            if (!IsConnected || job == null)
                return;

            try
            {
                await _client.StartJobsAsync(new List<string> { job.JobName });
                RefreshJobs(); // Refresh to get updated status
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting job: {ex.Message}");
            }
        }

        private async void PauseJob(RemoteJobViewModel job)
        {
            if (!IsConnected || job == null)
                return;

            try
            {
                await _client.PauseJobsAsync(new List<string> { job.JobName });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error pausing job: {ex.Message}");
            }
        }

        private async void ResumeJob(RemoteJobViewModel job)
        {
            if (!IsConnected || job == null)
                return;

            try
            {
                await _client.ResumeJobsAsync(new List<string> { job.JobName });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resuming job: {ex.Message}");
            }
        }

        private async void StopJob(RemoteJobViewModel job)
        {
            if (!IsConnected || job == null)
                return;

            try
            {
                await _client.StopJobsAsync(new List<string> { job.JobName });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping job: {ex.Message}");
            }
        }

        private void Client_ConnectionStatusChanged(object sender, string e)
        {
            ConnectionStatus = e;
        }

        private void Client_JobStatusesReceived(object sender, List<JobState> e)
        {
            try
            {
                // Check if we actually received job states
                if (e == null || e.Count == 0)
                {
                    Debug.WriteLine("Received empty job states list");
                    return;
                }
                
                UpdateJobList(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing received job states: {ex.Message}");
                ConnectionStatus = $"Error updating jobs: {ex.Message}";
            }
        }

        private void Client_ErrorOccurred(object sender, Exception e)
        {
            Debug.WriteLine($"Error: {e.Message}");
            ConnectionStatus = $"Error: {e.Message}";
        }

        private bool HasSelectedJobs()
        {
            return RemoteJobs?.Count > 0 && RemoteJobs.Any(j => j.IsSelected);
        }

        private async void StartSelectedJobs()
        {
            if (!IsConnected)
                return;

            try
            {
                var selectedJobs = RemoteJobs.Where(j => j.IsSelected).Select(j => j.JobName).ToList();
                if (selectedJobs.Count > 0)
                {
                    await _client.StartJobsAsync(selectedJobs);
                    RefreshJobs();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting selected jobs: {ex.Message}");
            }
        }

        private async void PauseSelectedJobs()
        {
            if (!IsConnected)
                return;

            try
            {
                var selectedJobs = RemoteJobs.Where(j => j.IsSelected).Select(j => j.JobName).ToList();
                if (selectedJobs.Count > 0)
                {
                    await _client.PauseJobsAsync(selectedJobs);
                    RefreshJobs();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error pausing selected jobs: {ex.Message}");
            }
        }

        private async void ResumeSelectedJobs()
        {
            if (!IsConnected)
                return;

            try
            {
                var selectedJobs = RemoteJobs.Where(j => j.IsSelected).Select(j => j.JobName).ToList();
                if (selectedJobs.Count > 0)
                {
                    await _client.ResumeJobsAsync(selectedJobs);
                    RefreshJobs();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resuming selected jobs: {ex.Message}");
            }
        }

        private async void StopSelectedJobs()
        {
            if (!IsConnected)
                return;

            try
            {
                var selectedJobs = RemoteJobs.Where(j => j.IsSelected).Select(j => j.JobName).ToList();
                if (selectedJobs.Count > 0)
                {
                    await _client.StopJobsAsync(selectedJobs);
                    RefreshJobs();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping selected jobs: {ex.Message}");
            }
        }
        
        // IEventListener implementation
        public void OnJobStatusChanged(JobStatus status)
        {
            if (status != null && IsConnected)
            {
                Debug.WriteLine($"RemoteControlViewModel.OnJobStatusChanged: Job '{status.BackupJob?.Name}' state changed to {status.State}");
                
                // Use the dispatcher to ensure we're on the UI thread
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => RefreshJobs());
                }
                else
                {
                    RefreshJobs();
                }
            }
        }

        public void OnBusinessSoftwareStateChanged(bool isRunning)
        {
            // Not needed for this implementation
        }

        public void OnLaunchJobsRequested(List<string> jobNames)
        {
            if (IsConnected)
            {
                RefreshJobs();
            }
        }

        public void OnPauseJobsRequested(List<string> jobNames)
        {
            if (IsConnected)
            {
                RefreshJobs();
            }
        }

        public void OnResumeJobsRequested(List<string> jobNames)
        {
            if (IsConnected)
            {
                RefreshJobs();
            }
        }

        public void OnStopJobsRequested(List<string> jobNames)
        {
            if (IsConnected)
            {
                RefreshJobs();
            }
        }

        // IDisposable implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unregister from EventManager
                    _eventManager.RemoveListener(this);
                    
                    // Disconnect from server
                    _client?.Disconnect();
                }

                _disposed = true;
            }
        }

        ~RemoteControlViewModel()
        {
            Dispose(false);
        }

        /// <summary>
        /// Cleans up resources used by this view model
        /// </summary>
        public void Cleanup()
        {
            Dispose();
        }
    }
}