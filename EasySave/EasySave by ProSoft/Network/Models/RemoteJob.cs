using EasySave_by_ProSoft.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySave_by_ProSoft.Network.Models
{
    /// <summary>
    /// Model representing a remote backup job status
    /// </summary>
    public class RemoteJob : INotifyPropertyChanged
    {
        private string _name;
        private string _sourcePath;
        private string _targetPath;
        private BackupType _type;
        private BackupState _state;
        private int _totalFiles;
        private long _totalSize;
        private int _remainingFiles;
        private long _remainingSize;
        private string _currentSourceFile;
        private string _currentTargetFile;
        private DateTime _startTime;
        private DateTime? _endTime;
        private bool _isSelected;

        #region Properties
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { if (_sourcePath != value) { _sourcePath = value; OnPropertyChanged(); } }
        }

        public string TargetPath
        {
            get => _targetPath;
            set { if (_targetPath != value) { _targetPath = value; OnPropertyChanged(); } }
        }

        public BackupType Type
        {
            get => _type;
            set { if (_type != value) { _type = value; OnPropertyChanged(); } }
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
                    OnPropertyChanged(nameof(StatusDisplay));
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(EstimatedTimeRemaining));
                }
            }
        }

        public int TotalFiles
        {
            get => _totalFiles;
            set
            {
                if (_totalFiles != value)
                {
                    _totalFiles = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProcessedFiles));
                }
            }
        }

        public long TotalSize
        {
            get => _totalSize;
            set
            {
                if (_totalSize != value)
                {
                    _totalSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(FormattedTotalSize));
                }
            }
        }

        public int RemainingFiles
        {
            get => _remainingFiles;
            set
            {
                if (_remainingFiles != value)
                {
                    _remainingFiles = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProcessedFiles));
                }
            }
        }

        public long RemainingSize
        {
            get => _remainingSize;
            set
            {
                if (_remainingSize != value)
                {
                    _remainingSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(TransferredSize));
                }
            }
        }

        public string CurrentSourceFile
        {
            get => _currentSourceFile;
            set { if (_currentSourceFile != value) { _currentSourceFile = value; OnPropertyChanged(); } }
        }

        public string CurrentTargetFile
        {
            get => _currentTargetFile;
            set { if (_currentTargetFile != value) { _currentTargetFile = value; OnPropertyChanged(); } }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ElapsedTime));
                }
            }
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ElapsedTime));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }
        #endregion

        #region Calculated Properties
        public int ProcessedFiles => Math.Max(0, TotalFiles - RemainingFiles);

        public long TransferredSize => TotalSize - RemainingSize;

        public double ProgressPercentage
        {
            get
            {
                if (TotalSize <= 0) return (State == BackupState.Completed && TotalFiles == 0) ? 100 : 0;
                if (State == BackupState.Completed) return 100;
                return Math.Round((double)TransferredSize / TotalSize * 100, 2);
            }
        }

        public string FormattedTotalSize
        {
            get
            {
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                if (TotalSize >= GB)
                    return $"{TotalSize / (double)GB:F2} GB";
                else if (TotalSize >= MB)
                    return $"{TotalSize / (double)MB:F2} MB";
                else if (TotalSize >= KB)
                    return $"{TotalSize / (double)KB:F2} KB";
                else
                    return $"{TotalSize} B";
            }
        }

        public TimeSpan ElapsedTime => (EndTime ?? DateTime.Now) - StartTime;

        public TimeSpan EstimatedTimeRemaining
        {
            get
            {
                if (State != BackupState.Running || ElapsedTime.TotalSeconds < 1 || TransferredSize <= 0)
                    return TimeSpan.Zero;

                double bytesPerSecond = TransferredSize / ElapsedTime.TotalSeconds;
                if (bytesPerSecond <= 0) return TimeSpan.Zero;

                return TimeSpan.FromSeconds(RemainingSize / bytesPerSecond);
            }
        }

        public string StatusDisplay => State switch
        {
            BackupState.Waiting => "En attente",
            BackupState.Running => "En cours",
            BackupState.Paused => "En pause",
            BackupState.Completed => "Terminé",
            BackupState.Error => "Erreur",
            _ => "Inconnu"
        };
        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}