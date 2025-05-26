using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Network.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasySave_by_ProSoft.Network.Services
{
    /// <summary>
    /// Interface for network connection services following MVVM pattern
    /// </summary>
    public interface INetworkConnectionService : IDisposable
    {
        /// <summary>
        /// Event raised when connection status changes
        /// </summary>
        event EventHandler<string> ConnectionStatusChanged;
        
        /// <summary>
        /// Event raised when status message changes
        /// </summary>
        event EventHandler<string> StatusMessageChanged;
        
        /// <summary>
        /// Event raised when job statuses are updated
        /// </summary>
        event EventHandler<List<JobState>> JobStatusesUpdated;
        
        /// <summary>
        /// Event raised when business software running state changes
        /// </summary>
        event EventHandler<bool> BusinessSoftwareStateChanged;
        
        /// <summary>
        /// Gets the connection state
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Gets whether an operation is currently in progress
        /// </summary>
        bool IsOperationInProgress { get; }
        
        /// <summary>
        /// Connects to the server
        /// </summary>
        /// <param name="serverAddress">Server address</param>
        /// <param name="serverPort">Server port</param>
        Task ConnectAsync(string serverAddress, int serverPort);
        
        /// <summary>
        /// Disconnects from the server
        /// </summary>
        Task DisconnectAsync();
        
        /// <summary>
        /// Requests job status update from server
        /// </summary>
        Task RequestJobStatusUpdateAsync();
        
        /// <summary>
        /// Sends a command to the server
        /// </summary>
        /// <param name="command">Command to send</param>
        Task SendCommandAsync(RemoteCommand command);
        Task SendCommandAsync(Models.RemoteCommand cmd);
    }
}