using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// View model for managing backup jobs
    /// </summary>
    public class BackupJobsViewModel : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        private ObservableCollection<BackupJob> _jobs;
        public ObservableCollection<BackupJob> Jobs
        {
            get => _jobs;
            set
            {
                _jobs = value;
                OnPropertyChanged();
            }
        }
        
        private BackupJob? _selectedJob;
        public BackupJob? SelectedJob
        {
            get => _selectedJob;
            set { _selectedJob = value; OnPropertyChanged(); }
        }
        
        private List<BackupJob> _selectedJobs = new List<BackupJob>();
        /// <summary>
        /// Gets or sets the list of selected backup jobs for multiple execution
        /// </summary>
        public List<BackupJob> SelectedJobs
        {
            get => _selectedJobs;
            set { _selectedJobs = value; OnPropertyChanged(); }
        }

        public ICommand CreateJobCommand { get; }
        public ICommand LaunchJobCommand { get; }
        public ICommand LaunchMultipleJobsCommand { get; }
        public ICommand RemoveJobCommand { get; }

        public BackupJobsViewModel(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _jobs = new ObservableCollection<BackupJob>();
            
            CreateJobCommand = new RelayCommand(_ => CreateJob(), _ => true);
            LaunchJobCommand = new RelayCommand(_ => LaunchSelectedJob(), _ => SelectedJob != null);
            LaunchMultipleJobsCommand = new RelayCommand(_ => LaunchMultipleJobs(), _ => SelectedJobs != null && SelectedJobs.Count > 0);
            RemoveJobCommand = new RelayCommand(_ => RemoveSelectedJob(), _ => SelectedJob != null);
            
            LoadJobs();
        }

        /// <summary>
        /// Loads all jobs from the BackupManager and updates the observable collection
        /// </summary>
        public void LoadJobs()
        {
            // Ensure BackupManager has the latest data from JSON
            _backupManager.LoadJobs();
            
            Jobs.Clear();
            var jobs = _backupManager.GetAllJobs();
            foreach (var job in jobs)
            {
                Jobs.Add(job);
            }
            OnPropertyChanged(nameof(Jobs));
        }

        /// <summary>
        /// Adds a job to the observable collection
        /// </summary>
        /// <param name="job">The job to add</param>
        public void JobAdded(BackupJob job)
        {
            if (!Jobs.Contains(job))
            {
                Jobs.Add(job);
                OnPropertyChanged(nameof(Jobs));
            }
        }

        /// <summary>
        /// Opens the job creation panel
        /// </summary>
        private void CreateJob()
        {
            // This method can be used to display a job creation dialog
            // See the equivalent method in BackupJobsView.xaml.cs (CreateNewJob_Click)
        }

        /// <summary>
        /// Launches the selected backup job
        /// </summary>
        private void LaunchSelectedJob()
        {
            if (SelectedJob != null)
            {
                SelectedJob.Start();
            }
        }
        
        /// <summary>
        /// Launches multiple backup jobs that are selected in the UI
        /// </summary>
        private void LaunchMultipleJobs()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
                return;
                
            // Get indices of selected jobs
            var jobIndices = new List<int>();
            foreach (var job in SelectedJobs)
            {
                int index = Jobs.IndexOf(job);
                if (index >= 0)
                {
                    jobIndices.Add(index);
                }
            }
            
            // Execute selected jobs using BackupManager
            if (jobIndices.Count > 0)
            {
                var jobIndicesRef = jobIndices; // Create a reference variable
                _backupManager.ExecuteJobs(ref jobIndicesRef);
            }
        }

        /// <summary>
        /// Removes the selected backup job
        /// </summary>
        private void RemoveSelectedJob()
        {
            if (SelectedJob != null)
            {
                int index = Jobs.IndexOf(SelectedJob);
                if (index >= 0)
                {
                    int indexRef = index; // Required for reference
                    if (_backupManager.RemoveJob(ref indexRef))
                    {
                        Jobs.Remove(SelectedJob);
                        OnPropertyChanged(nameof(Jobs));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Simple RelayCommand implementation for MVVM pattern
    /// </summary>
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