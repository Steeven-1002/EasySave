using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySave_by_ProSoft.Models
{
    /// <summary>
    /// Represents a remote job in the UI, created from JobState data received over the network
    /// </summary>
    public class RemoteJob : INotifyPropertyChanged
    {
        private string _name;
        private string _source;
        private string _destination;
        private BackupState _state;
        private double _progress;
        private long _totalFiles;
        private long _filesProcessed;
        private long _totalSize;
        private long _processedSize;
        private TimeSpan _elapsedTime;
        private string _currentFile;
        private DateTime _lastUpdate;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Source
        {
            get => _source;
            set { _source = value; OnPropertyChanged(); }
        }

        public string Destination
        {
            get => _destination;
            set { _destination = value; OnPropertyChanged(); }
        }

        public BackupState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public long TotalFiles
        {
            get => _totalFiles;
            set { _totalFiles = value; OnPropertyChanged(); }
        }

        public long FilesProcessed
        {
            get => _filesProcessed;
            set { _filesProcessed = value; OnPropertyChanged(); }
        }

        public long TotalSize
        {
            get => _totalSize;
            set { _totalSize = value; OnPropertyChanged(); }
        }

        public long ProcessedSize
        {
            get => _processedSize;
            set { _processedSize = value; OnPropertyChanged(); }
        }

        public TimeSpan ElapsedTime
        {
            get => _elapsedTime;
            set { _elapsedTime = value; OnPropertyChanged(); }
        }

        public string CurrentFile
        {
            get => _currentFile;
            set { _currentFile = value; OnPropertyChanged(); }
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set { _lastUpdate = value; OnPropertyChanged(); }
        }

        public static RemoteJob FromJobState(JobState state)
        {
            if (state == null)
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] FromJobState called with null state");
                throw new ArgumentNullException(nameof(state));
            }

            try
            {
                var remoteJob = new RemoteJob
                {
                    Name = state.JobName,
                    Source = state.SourcePath ?? string.Empty,
                    Destination = state.TargetPath ?? string.Empty,
                    State = state.State,
                    Progress = state.ProgressPercentage,
                    TotalFiles = state.TotalFiles,
                    TotalSize = state.TotalSize,
                    FilesProcessed = state.RemainingFiles == 0 ? state.TotalFiles : state.TotalFiles - state.RemainingFiles,
                    LastUpdate = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Created RemoteJob: {remoteJob.Name}, State: {remoteJob.State}, Progress: {remoteJob.Progress}%");
                return remoteJob;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error in FromJobState: {ex.Message}");
                throw;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}