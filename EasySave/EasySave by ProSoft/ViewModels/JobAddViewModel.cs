using EasySave_by_ProSoft.Models;
using EasySave_by_ProSoft.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace EasySave_by_ProSoft.ViewModels
{
    public class JobAddViewModel : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        private readonly IDialogService _dialogService;
        private string _name = string.Empty;
        private string _sourcePath = string.Empty;
        private string _targetPath = string.Empty;
        private BackupType _type = BackupType.Full;

        // Event for validation errors
        public event Action<string>? ValidationError;

        // Event for successful job creation
        public event Action<string>? JobCreated;

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

        public ICommand AddJobCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<BackupJob>? JobAdded;
        public event Action? JobCancelled;

        public JobAddViewModel(BackupManager backupManager) : this(backupManager, new DialogService()) { }

        public JobAddViewModel(BackupManager backupManager, IDialogService dialogService)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            AddJobCommand = new RelayCommand(_ => AddJob(), _ => CanAddJob());
            CancelCommand = new RelayCommand(_ => CancelJob());
        }

        /// <summary>
        /// Validates if all required fields for a job are provided
        /// </summary>
        public bool ValidateJobInputs()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationError?.Invoke("Job name cannot be empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(SourcePath))
            {
                ValidationError?.Invoke("Source path cannot be empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(TargetPath))
            {
                ValidationError?.Invoke("Target path cannot be empty.");
                return false;
            }

            return true;
        }

        private void AddJob()
        {
            if (!ValidateJobInputs())
                return;

            string sourcePath = SourcePath;
            string targetPath = TargetPath;
            BackupType type = Type;

            try
            {
                var job = _backupManager.AddJob(Name, ref sourcePath, ref targetPath, ref type);
                JobAdded?.Invoke(job);
                JobCreated?.Invoke(Localization.Resources.MessageNewJobValidated);

                // Reset input fields after successful job creation
                ResetInputFields();
            }
            catch (Exception ex)
            {
                ValidationError?.Invoke($"Error creating job: {ex.Message}");
            }
        }

        private void CancelJob()
        {
            // Reset all fields and notify the view that the operation was cancelled
            ResetInputFields();
            JobCancelled?.Invoke();
        }

        private void ResetInputFields()
        {
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