using EasySave_by_ProSoft.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace EasySave_by_ProSoft.ViewModels
{

    public class JobAddViewModel : INotifyPropertyChanged, JobEventListeners
    {
        private readonly BackupManager _backupManager;
        private string _name = string.Empty;
        private string _sourcePath = string.Empty;
        private string _targetPath = string.Empty;
        private BackupType _type = BackupType.Full;
        private BackupState _status = BackupState.Initialise;
        private int _progressPercentage = 0;
        private JobEventManager _jobEventManager = JobEventManager.Instance;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); }
        }

        public string TargetPath
        {
            get => _targetPath;
            set { _targetPath = value; OnPropertyChanged(); }
        }

        public BackupType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public BackupState Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set { _progressPercentage = value; OnPropertyChanged(); }
        }

        public ICommand AddJobCommand { get; }

        public event Action<BackupJob>? JobAdded;

        public JobAddViewModel(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            AddJobCommand = new RelayCommand(_ => AddJob(), _ => CanAddJob());
            _jobEventManager.AddListener(this);
        }
        public void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile, double transfertDuration, double encryptionTimeMs, string details = null)
        {
            // Update the job status based on the event data
            Status = newState;
            
            // Calculate progress percentage properly based on remaining vs total
            if (totalSize > 0)
            {
                ProgressPercentage = (int)Math.Round((double)(totalSize - remainingSize) / totalSize * 100, 0);
            }
            else if (totalFiles > 0)
            {
                ProgressPercentage = (int)Math.Round((double)(totalFiles - remainingFiles) / totalFiles * 100, 0);
            }
            else
            {
                ProgressPercentage = 0;
            }
            
            // Make sure to notify property changes
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(ProgressPercentage));
        }

        private void AddJob()
        {
            string sourcePath = SourcePath;
            string targetPath = TargetPath;
            BackupType type = Type;

            var job = _backupManager.AddJob(Name, ref sourcePath, ref targetPath, ref type);

            JobAdded?.Invoke(job);

            Name = string.Empty;
            SourcePath = string.Empty;
            TargetPath = string.Empty;
            Type = BackupType.Full;
        }

        private bool CanAddJob()
        {
            return !string.IsNullOrWhiteSpace(Name)
                && !string.IsNullOrWhiteSpace(SourcePath)
                && !string.IsNullOrWhiteSpace(TargetPath);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}