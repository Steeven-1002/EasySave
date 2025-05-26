using System.Collections.Generic;
using System.Linq;

namespace EasySave_by_ProSoft.Network.Models
{
    /// <summary>
    /// Represents a command sent between client and server
    /// </summary>
    public class RemoteCommand
    {
        /// <summary>
        /// The type of command being sent
        /// </summary>
        public string CommandType { get; set; }
        
        /// <summary>
        /// The name of a job to act upon (for backward compatibility)
        /// </summary>
        public string JobName { get; set; }
        
        /// <summary>
        /// List of job names to act upon
        /// </summary>
        public List<string> JobNames { get; set; }
        
        /// <summary>
        /// Additional parameters for the command
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }
        
        /// <summary>
        /// Creates a new command with default values
        /// </summary>
        public RemoteCommand()
        {
            CommandType = "RequestStatusUpdate";
            JobName = string.Empty;
            JobNames = new List<string>();
            Parameters = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Creates a new command with the specified command type
        /// </summary>
        /// <param name="commandType">The type of command</param>
        public RemoteCommand(string commandType)
        {
            CommandType = string.IsNullOrWhiteSpace(commandType) ? "RequestStatusUpdate" : commandType;
            JobName = string.Empty;
            JobNames = new List<string>();
            Parameters = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Creates a new command for specified jobs
        /// </summary>
        /// <param name="commandType">The type of command</param>
        /// <param name="jobNames">List of job names to act upon</param>
        public RemoteCommand(string commandType, IEnumerable<string> jobNames)
        {
            CommandType = string.IsNullOrWhiteSpace(commandType) ? "RequestStatusUpdate" : commandType;
            JobName = string.Empty;
            JobNames = jobNames?.ToList() ?? new List<string>();
            Parameters = new Dictionary<string, object>();
        }
    }
}