using System;
using System.Diagnostics;

using System.Windows;

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
                // Extract process name without extension
                string processName = System.IO.Path.GetFileNameWithoutExtension(monitoredApplication);
                
                // Check if any process with this name is running
                Process[] processes = Process.GetProcessesByName(processName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
