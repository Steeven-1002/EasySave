using EasySave_by_ProSoft.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// ViewModel representing a backup job on a remote server
    /// </summary>
    public class RemoteJobViewModel : INotifyPropertyChanged
    {
        // Basic properties
        private string _jobName;
        private string _sourcePath;
        private string _targetPath;
        private BackupType _type;
        private BackupState _state;
        private double _progressPercentage;
        private string _currentSourceFile;
        private string _currentTargetFile;
        private DateTime _lastUpdateTime;
        private string _details;
        private bool _isSelected;

        public string JobName
        {
            get => _jobName;
            set
            {
                if (_jobName != value)
                {
                    _jobName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set
            {
                if (_sourcePath != value)
                {
                    _sourcePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TargetPath
        {
            get => _targetPath;
            set
            {
                if (_targetPath != value)
                {
                    _targetPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public BackupType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                }
            }
        }

        public BackupState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StateDisplay));
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanPause));
                    OnPropertyChanged(nameof(CanResume));
                    OnPropertyChanged(nameof(CanStop));
                }
            }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                if (_progressPercentage != value)
                {
                    _progressPercentage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentSourceFile
        {
            get => _currentSourceFile;
            set
            {
                if (_currentSourceFile != value)
                {
                    _currentSourceFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentTargetFile
        {
            get => _currentTargetFile;
            set
            {
                if (_currentTargetFile != value)
                {
                    _currentTargetFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set
            {
                if (_lastUpdateTime != value)
                {
                    _lastUpdateTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Details
        {
            get => _details;
            set
            {
                if (_details != value)
                {
                    _details = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        // Derived properties
        public string StateDisplay
        {
            get
            {
                return State switch
                {
                    BackupState.Initialise => "Ready",
                    BackupState.Waiting => "Ready",
                    BackupState.Running => "Running",
                    BackupState.Paused => "Paused",
                    BackupState.Completed => "Completed",
                    BackupState.Error => "Error",
                    _ => "Unknown"
                };
            }
        }

        // Action availability
        public bool CanStart => State == BackupState.Initialise || State == BackupState.Completed || State == BackupState.Error;
        public bool CanPause => State == BackupState.Running;
        public bool CanResume => State == BackupState.Paused;
        public bool CanStop => State == BackupState.Running || State == BackupState.Paused;

        /// <summary>
        /// Initializes a new instance of RemoteJobViewModel from a JobState
        /// </summary>
        public RemoteJobViewModel(JobState jobState)
        {
            Update(jobState);
        }

        /// <summary>
        /// Updates the job view model with the latest state
        /// </summary>
        public void Update(JobState jobState)
        {
            if (jobState == null)
                return;

            JobName = jobState.JobName;
            SourcePath = jobState.SourcePath;
            TargetPath = jobState.TargetPath;
            Type = jobState.Type;
            State = jobState.State;
            ProgressPercentage = jobState.ProgressPercentage;
            CurrentSourceFile = jobState.CurrentSourceFile;
            CurrentTargetFile = jobState.CurrentTargetFile;
            LastUpdateTime = jobState.Timestamp;
            Details = jobState.Details;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}