using EasySave_by_ProSoft.Models;
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
            Debug.WriteLine("EventManager initialized - Socket server started");
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
        }

        /// <summary>
        /// Notifies listeners about job launch requests
        /// </summary>
        /// <param name="jobNames">List of job names to launch</param>
        public void NotifyLaunchJobsRequested(List<string> jobNames)
        {
            Debug.WriteLine($"EventManager: Notifying about launch request for jobs: {string.Join(", ", jobNames)}");

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

        /// <summary>
        /// Notifies listeners about job pause requests
        /// </summary>
        /// <param name="jobNames">List of job names to pause</param>
        public void NotifyPauseJobsRequested(List<string> jobNames)
        {
            Debug.WriteLine($"EventManager: Notifying about pause request for jobs: {string.Join(", ", jobNames)}");

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

        /// <summary>
        /// Notifies listeners about job resume requests
        /// </summary>
        /// <param name="jobNames">List of job names to resume</param>
        public void NotifyResumeJobsRequested(List<string> jobNames)
        {
            Debug.WriteLine($"EventManager: Notifying about resume request for jobs: {string.Join(", ", jobNames)}");

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

        /// <summary>
        /// Notifies listeners about job stop requests
        /// </summary>
        /// <param name="jobNames">List of job names to stop</param>
        public void NotifyStopJobsRequested(List<string> jobNames)
        {
            Debug.WriteLine($"EventManager: Notifying about stop request for jobs: {string.Join(", ", jobNames)}");

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