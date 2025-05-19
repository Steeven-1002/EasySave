using System.Diagnostics;
using System.IO;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Monitors business application to check if it's running
    /// </summary>
    public class BusinessApplicationMonitor
    {
        private string monitoredApplication;

        /// <summary>
        /// Initializes a new instance of the BusinessApplicationMonitor
        /// </summary>
        /// <param name="applicationName">Name of the business software to monitor</param>
        public BusinessApplicationMonitor(string applicationName)
        {
            monitoredApplication = applicationName;
        }

        /// <summary>
        /// Checks if the monitored business application is currently running
        /// </summary>
        /// <returns>True if the application is running, otherwise false</returns>
        public bool IsRunning()
        {
            if (string.IsNullOrWhiteSpace(monitoredApplication))
                return false;

            try
            {
                // Try multiple approaches to find the process
                string processName = Path.GetFileNameWithoutExtension(monitoredApplication);

                // Log the process we're looking for in debug mode
                Debug.WriteLine($"Monitoring for business application: '{monitoredApplication}', process name: '{processName}'");

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
                    Debug.WriteLine($"Trying alternative process name: '{shortName}'");
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

                    if (processes.Length > 0)
                    {
                        Debug.WriteLine($"Found process by partial name match: {processes[0].ProcessName}");
                    }
                }

                if (processes.Length > 0)
                {
                    Debug.WriteLine($"Business application '{monitoredApplication}' is running. Found {processes.Length} matching processes.");
                }
                else
                {
                    Debug.WriteLine($"Business application '{monitoredApplication}' is not running. No matching processes found.");
                }

                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error monitoring business application '{monitoredApplication}': {ex.Message}",
                    "Business Application Monitor Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
