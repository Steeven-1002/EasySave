using System.Diagnostics;
using System.IO;
using System.Threading;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Monitors business application to check if it's running
    /// </summary>
    public class BusinessApplicationMonitor
    {
        private string monitoredApplication;
        private Thread monitoringThread;
        private CancellationTokenSource cancellationTokenSource;
        private readonly object stateLock = new();
        private bool isApplicationRunning = false;
        private bool isMonitoring = false;
        private int checkIntervalMs = 1000; // Default to 1 second

        /// <summary>
        /// Event triggered when the business application starts
        /// </summary>
        public event EventHandler BusinessAppStarted;

        /// <summary>
        /// Event triggered when the business application stops
        /// </summary>
        public event EventHandler BusinessAppStopped;

        /// <summary>
        /// Gets the name of the business software being monitored
        /// </summary>
        public string BusinessSoftwareName => monitoredApplication;

        /// <summary>
        /// Initializes a new instance of the BusinessApplicationMonitor
        /// </summary>
        /// <param name="applicationName">Name of the business software to monitor</param>
        public BusinessApplicationMonitor(string applicationName)
        {
            monitoredApplication = applicationName;
        }

        /// <summary>
        /// Starts the continuous monitoring of the business application in a separate thread
        /// </summary>
        public void StartMonitoring()
        {
            if (isMonitoring || string.IsNullOrWhiteSpace(monitoredApplication))
                return;

            lock (stateLock)
            {
                if (isMonitoring)
                    return;

                cancellationTokenSource = new CancellationTokenSource();
                monitoringThread = new Thread(() => MonitoringLoop(cancellationTokenSource.Token))
                {
                    IsBackground = true,
                    Name = "BusinessAppMonitor"
                };

                isMonitoring = true;
                monitoringThread.Start();
                Debug.WriteLine($"Started monitoring thread for business application: '{monitoredApplication}'");
            }
        }

        /// <summary>
        /// Stops the monitoring thread
        /// </summary>
        public void StopMonitoring()
        {
            if (!isMonitoring)
                return;

            lock (stateLock)
            {
                if (!isMonitoring)
                    return;

                Debug.WriteLine($"Stopping monitoring thread for business application: '{monitoredApplication}'");
                cancellationTokenSource?.Cancel();
                monitoringThread?.Join(3000); // Wait up to 3 seconds for a clean shutdown

                isMonitoring = false;
                monitoringThread = null;
                cancellationTokenSource = null;
                Debug.WriteLine($"Monitoring thread stopped for business application: '{monitoredApplication}'");
            }
        }

        /// <summary>
        /// The main loop that runs in the background thread to monitor the application
        /// </summary>
        private void MonitoringLoop(CancellationToken token)
        {
            bool previousState = false;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool currentState = CheckIsRunning();

                    // Thread-safe state update and event triggering
                    lock (stateLock)
                    {
                        if (currentState != previousState)
                        {
                            isApplicationRunning = currentState;

                            // Trigger the appropriate event when state changes
                            if (currentState && !previousState)
                            {
                                Debug.WriteLine($"Business application '{monitoredApplication}' has started.");
                                BusinessAppStarted?.Invoke(this, EventArgs.Empty);
                            }
                            else if (!currentState && previousState)
                            {
                                Debug.WriteLine($"Business application '{monitoredApplication}' has stopped.");
                                BusinessAppStopped?.Invoke(this, EventArgs.Empty);
                            }

                            previousState = currentState;
                        }
                    }

                    // Sleep between checks to avoid high CPU usage
                    Thread.Sleep(checkIntervalMs);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the token is canceled
                Debug.WriteLine("Monitoring thread was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in monitoring thread: {ex.Message}");
            }
            finally
            {
                lock (stateLock)
                {
                    isMonitoring = false;
                }
            }
        }

        /// <summary>
        /// Checks if the monitored business application is currently running
        /// Thread-safe method that can be called from any thread
        /// </summary>
        /// <returns>True if the application is running, otherwise false</returns>
        public bool IsRunning()
        {
            // If monitoring is active, return the cached state
            lock (stateLock)
            {
                if (isMonitoring)
                    return isApplicationRunning;
            }

            // If not actively monitoring, perform a direct check
            return CheckIsRunning();
        }

        /// <summary>
        /// Internal method that performs the actual checking logic
        /// </summary>
        private bool CheckIsRunning()
        {
            if (string.IsNullOrWhiteSpace(monitoredApplication))
                return false;

            try
            {
                // Try multiple approaches to find the process
                string processName = Path.GetFileNameWithoutExtension(monitoredApplication);

                // Check if any process with this name is running
                Process[] processes = Process.GetProcessesByName(processName);

                // Also try with lowercase process name as some processes register differently
                if (processes.Length == 0)
                {
                    processes = Process.GetProcessesByName(processName.ToLower());
                }

                // Try just the first part of the name (before spaces) as another fallback
                if (processes.Length == 0 && processName.Contains(" "))
                {
                    string shortName = processName.Split(' ')[0];
                    processes = Process.GetProcessesByName(shortName);
                }

                // Try to get all processes and search for partial matches
                if (processes.Length == 0)
                {
                    Process[] allProcesses = Process.GetProcesses();
                    processes = allProcesses.Where(p =>
                    {
                        try
                        {
                            return p.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    }).ToArray();
                }

                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if business application is running: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets the interval between application status checks
        /// </summary>
        /// <param name="intervalMs">Interval in milliseconds</param>
        public void SetCheckInterval(int intervalMs)
        {
            if (intervalMs >= 100) // Minimum 100ms to avoid excessive CPU usage
            {
                checkIntervalMs = intervalMs;
            }
        }
    }
}
