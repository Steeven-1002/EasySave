using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.ViewModels
{
    public class BackupJobsViewModel : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        public ObservableCollection<BackupJob> Jobs { get; set; } = new();
        private BackupJob? _selectedJob;
        public BackupJob? SelectedJob
        {
            get => _selectedJob;
            set { _selectedJob = value; OnPropertyChanged(); }
        }

        public ICommand CreateJobCommand { get; }
        public ICommand LaunchJobCommand { get; }
        public ICommand RemoveJobCommand { get; }

        public BackupJobsViewModel(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            
            CreateJobCommand = new RelayCommand(_ => CreateJob(), _ => true);
            LaunchJobCommand = new RelayCommand(_ => LaunchSelectedJob(), _ => SelectedJob != null);
            RemoveJobCommand = new RelayCommand(_ => RemoveSelectedJob(), _ => SelectedJob != null);
            
            LoadJobs();
        }

        private void LoadJobs()
        {
            Jobs.Clear();
            var jobs = _backupManager.GetAllJobs();
            foreach (var job in jobs)
            {
                Jobs.Add(job);
            }
        }

        public void JobAdded(BackupJob job)
        {
            if (!Jobs.Contains(job))
            {
                Jobs.Add(job);
            }
        }

        private void CreateJob()
        {
            // TODO: Add logic to create a new job (show dialog or use bound properties)
        }

        private void LaunchSelectedJob()
        {
            if (SelectedJob != null)
            {
                SelectedJob.Start();
            }
        }

        private void RemoveSelectedJob()
        {
            if (SelectedJob != null)
            {
                int index = Jobs.IndexOf(SelectedJob);
                if (index >= 0)
                {
                    int indexRef = index; // Capture the index for the lambda
                    if (_backupManager.RemoveJob(ref indexRef))
                    {
                        Jobs.Remove(SelectedJob);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Simple RelayCommand implementation for MVVM
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}