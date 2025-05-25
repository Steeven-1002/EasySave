using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace EasySave_by_ProSoft.Network
{
    /// <summary>
    /// Monitors network bandwidth consumption and provides throttling capabilities
    /// </summary>
    public class NetworkMonitor
    {
        private readonly NetworkInterface _networkInterface;
        private long _lastBytesReceived;
        private long _lastBytesSent;
        private DateTime _lastCheckTime;
        private double _currentBandwidthKBps;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isMonitoring;

        public double CurrentBandwidthKBps => _currentBandwidthKBps;
        public event EventHandler<double> BandwidthUpdated;
        public event EventHandler<bool> BandwidthThresholdExceeded;

        public NetworkMonitor()
        {
            // Try to get the most active network interface
            _networkInterface = GetMostActiveNetworkInterface();
            _lastCheckTime = DateTime.Now;
            if (_networkInterface != null)
            {
                IPv4InterfaceStatistics stats = _networkInterface.GetIPv4Statistics();
                _lastBytesReceived = stats.BytesReceived;
                _lastBytesSent = stats.BytesSent;
            }
        }

        private NetworkInterface GetMostActiveNetworkInterface()
        {
            NetworkInterface mostActiveInterface = null;
            long highestByteCount = 0;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    IPv4InterfaceStatistics stats = ni.GetIPv4Statistics();
                    long totalBytes = stats.BytesReceived + stats.BytesSent;

                    if (totalBytes > highestByteCount)
                    {
                        mostActiveInterface = ni;
                        highestByteCount = totalBytes;
                    }
                }
            }

            return mostActiveInterface;
        }

        public void StartMonitoring(int updateIntervalMs = 1000)
        {
            if (_isMonitoring || _networkInterface == null) return;

            _isMonitoring = true;
            _cancellationTokenSource = new CancellationTokenSource();
            
            Task.Run(() => MonitorNetworkAsync(updateIntervalMs, _cancellationTokenSource.Token));
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _cancellationTokenSource?.Cancel();
            _isMonitoring = false;
        }

        private async Task MonitorNetworkAsync(int updateIntervalMs, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    UpdateBandwidthUsage();
                    await Task.Delay(updateIntervalMs, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when token is canceled
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in network monitoring: {ex.Message}");
            }
            finally
            {
                _isMonitoring = false;
            }
        }

        private void UpdateBandwidthUsage()
        {
            if (_networkInterface == null) return;

            try
            {
                IPv4InterfaceStatistics stats = _networkInterface.GetIPv4Statistics();
                DateTime now = DateTime.Now;
                
                long bytesReceived = stats.BytesReceived;
                long bytesSent = stats.BytesSent;
                
                long bytesReceivedDelta = bytesReceived - _lastBytesReceived;
                long bytesSentDelta = bytesSent - _lastBytesSent;
                double seconds = (now - _lastCheckTime).TotalSeconds;
                
                if (seconds > 0)
                {
                    // Total throughput in KB/s
                    _currentBandwidthKBps = Math.Round((bytesReceivedDelta + bytesSentDelta) / 1024.0 / seconds, 2);
                    
                    BandwidthUpdated?.Invoke(this, _currentBandwidthKBps);
                }
                
                _lastBytesReceived = bytesReceived;
                _lastBytesSent = bytesSent;
                _lastCheckTime = now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating bandwidth usage: {ex.Message}");
            }
        }

        public bool IsBandwidthExceededThreshold(double thresholdKBps)
        {
            bool isExceeded = _currentBandwidthKBps > thresholdKBps;
            BandwidthThresholdExceeded?.Invoke(this, isExceeded);
            return isExceeded;
        }

        public string GetNetworkInterfaceName()
        {
            return _networkInterface?.Name ?? "No active network interface";
        }

        public string GetNetworkInterfaceDescription()
        {
            return _networkInterface?.Description ?? "No active network interface";
        }
    }
}