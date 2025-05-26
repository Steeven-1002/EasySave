using System.Collections.Generic;

namespace EasySave_by_ProSoft.Network.Models
{
    /// <summary>
    /// Represents a command to be sent to the remote server
    /// </summary>
    public class RemoteCommand
    {
        public string CommandType { get; set; }
        public string JobName { get; set; }
        public List<string> JobNames { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public RemoteCommand()
        {
            CommandType = string.Empty;
            JobName = string.Empty;
            JobNames = new List<string>();
            Parameters = new Dictionary<string, object>();
        }

        public RemoteCommand(string commandType)
        {
            CommandType = commandType ?? string.Empty;
            JobName = string.Empty;
            JobNames = new List<string>();
            Parameters = new Dictionary<string, object>();
        }

        public RemoteCommand(string commandType, string jobName) : this(commandType)
        {
            JobName = jobName;
        }

        public RemoteCommand(string commandType, List<string> jobNames) : this(commandType)
        {
            JobNames = jobNames ?? new List<string>();
        }
    }
}