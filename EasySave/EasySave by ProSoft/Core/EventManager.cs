using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network;
using System.Diagnostics;

namespace EasySave_by_ProSoft.Core
{
    /// <summary>
    /// Central event management system for the application
    /// </summary>
    public class EventManager
    {
        private static EventManager _instance;
        private static readonly object _lockObject = new();

        private readonly List<IEventListener> _listeners = new();
        private readonly SocketServer _socketServer;

        public static EventManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new EventManager();
                    }
                }
                return _instance;
            }
        }

        private EventManager()
        {
            _socketServer = new SocketServer();
            _socketServer.CommandReceived += SocketServer_CommandReceived;
            _socketServer.Start();
            Debug.WriteLine("EventManager initialized - Socket server started");
        }

        private void SocketServer_CommandReceived(object sender, RemoteCommand command)
        {
            Debug.WriteLine($"EventManager received command: {command.CommandType}");
            ProcessRemoteCommand(command);
        }

        public void AddListener(IEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
            {
                _listeners.Add(listener);
                Debug.WriteLine($"EventManager: Added listener of type {listener.GetType().Name}");
            }
        }

        public void RemoveListener(IEventListener listener)
        {
            if (listener != null && _listeners.Remove(listener))
            {
                Debug.WriteLine($"EventManager: Removed listener of type {listener.GetType().Name}");
            }
        }

        public void NotifyJobStatusChanged(JobStatus status)
        {
            if (status == null)
            {
                Debug.WriteLine("EventManager: Cannot notify with null JobStatus");
                return;
            }

            Debug.WriteLine($"EventManager: Notifying job status change for job '{status.BackupJob?.Name}' - State: {status.State}");

            foreach (var listener in _listeners.ToList()) // Use a copy to avoid modification issues during iteration
            {
                try
                {
                    listener.OnJobStatusChanged(status);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EventManager: Error notifying listener {listener.GetType().Name} about job status change: {ex.Message}");
                }
            }

            // Also notify remote clients
            _socketServer.BroadcastJobStatusesAsync();
        }

        public void NotifyBusinessSoftwareStateChanged(bool isRunning)
        {
            Debug.WriteLine($"EventManager: Notifying business software state change - IsRunning: {isRunning}");

            foreach (var listener in _listeners.ToList()) // Use a copy to avoid modification issues during iteration
            {
                try
                {
                    listener.OnBusinessSoftwareStateChanged(isRunning);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EventManager: Error notifying listener {listener.GetType().Name} about business software state change: {ex.Message}");
                }
            }

            // Also notify remote clients
            _socketServer.BroadcastBusinessAppStateAsync(isRunning);
        }

        private void ProcessRemoteCommand(RemoteCommand command)
        {
            switch (command.CommandType)
            {
                case "LaunchJobs":
                    Debug.WriteLine($"EventManager: Processing LaunchJobs command for {command.JobNames?.Count ?? 0} jobs");
                    NotifyLaunchJobs(command.JobNames);
                    break;
                case "PauseJobs":
                    Debug.WriteLine($"EventManager: Processing PauseJobs command for {command.JobNames?.Count ?? 0} jobs");
                    NotifyPauseJobs(command.JobNames);
                    break;
                case "ResumeJobs":
                    Debug.WriteLine($"EventManager: Processing ResumeJobs command for {command.JobNames?.Count ?? 0} jobs");
                    NotifyResumeJobs(command.JobNames);
                    break;
                case "StopJobs":
                    Debug.WriteLine($"EventManager: Processing StopJobs command for {command.JobNames?.Count ?? 0} jobs");
                    NotifyStopJobs(command.JobNames);
                    break;
                default:
                    Debug.WriteLine($"EventManager: Unknown command type: {command.CommandType}");
                    break;
            }
        }

        private void NotifyLaunchJobs(List<string> jobNames)
        {
            foreach (var listener in _listeners.ToList())
            {
                try
                {
                    listener.OnLaunchJobsRequested(jobNames);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EventManager: Error notifying listener {listener.GetType().Name} about launch request: {ex.Message}");
                }
            }
        }

        private void NotifyPauseJobs(List<string> jobNames)
        {
            foreach (var listener in _listeners.ToList())
            {
                try
                {
                    listener.OnPauseJobsRequested(jobNames);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EventManager: Error notifying listener {listener.GetType().Name} about pause request: {ex.Message}");
                }
            }
        }

        private void NotifyResumeJobs(List<string> jobNames)
        {
            foreach (var listener in _listeners.ToList())
            {
                try
                {
                    listener.OnResumeJobsRequested(jobNames);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EventManager: Error notifying listener {listener.GetType().Name} about resume request: {ex.Message}");
                }
            }
        }

        private void NotifyStopJobs(List<string> jobNames)
        {
            foreach (var listener in _listeners.ToList())
            {
                try
                {
                    listener.OnStopJobsRequested(jobNames);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EventManager: Error notifying listener {listener.GetType().Name} about stop request: {ex.Message}");
                }
            }
        }
    }

    public interface IEventListener
    {
        void OnJobStatusChanged(JobStatus status);
        void OnBusinessSoftwareStateChanged(bool isRunning);
        void OnLaunchJobsRequested(List<string> jobNames);
        void OnPauseJobsRequested(List<string> jobNames);
        void OnResumeJobsRequested(List<string> jobNames);
        void OnStopJobsRequested(List<string> jobNames);
    }
}