using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.ViewModels
{
    /// <summary>
    /// View model for managing backup jobs
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BackupManager _backupManager;
        private ObservableCollection<BackupJob> _jobs;
        private DispatcherTimer _statusUpdateTimer;
        private volatile bool _isLaunchingJobs = false;

        public ObservableCollection<BackupJob> Jobs
        {
            get => _jobs;
            set
            {
                _jobs = value;
                OnPropertyChanged();
            }
        }

        private List<BackupJob> _selectedJobs = new List<BackupJob>();
        /// <summary>
        /// Gets or sets the list of selected backup jobs for multiple execution
        /// </summary>
        public List<BackupJob> SelectedJobs
        {
            get => _selectedJobs;
            set
            {
                _selectedJobs = value;
                OnPropertyChanged();
                // Notify commands that depend on selection
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand CreateJobCommand { get; }
        public ICommand LaunchJobCommand { get; }
        public ICommand LaunchMultipleJobsCommand { get; }
        public ICommand RemoveJobCommand { get; }
        public ICommand PauseJobCommand { get; }
        public ICommand ResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }

        public MainViewModel(BackupManager backupManager)
        {
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _jobs = new ObservableCollection<BackupJob>();

            Predicate<object?> canLaunchPredicate = _ =>
               SelectedJobs != null &&
               SelectedJobs.Any(job => job.Status.State == BackupState.Initialise ||
                                       job.Status.State == BackupState.Error ||
                                       job.Status.State == BackupState.Completed) &&
               !_isLaunchingJobs; // Ne pas permettre le lancement si un lancement est d�j� en cours

            LaunchJobCommand = new RelayCommand(async _ => await LaunchSelectedJob(), canLaunchPredicate);
            LaunchMultipleJobsCommand = new RelayCommand(async _ => await LaunchSelectedJob(), canLaunchPredicate);

            CreateJobCommand = new RelayCommand(_ => CreateJob(), _ => true);
            // LaunchJobCommand = new RelayCommand(_ => LaunchSelectedJob(), _ => SelectedJob != null && SelectedJob.Count > 0);
            //LaunchMultipleJobsCommand = new RelayCommand(_ => LaunchMultipleJobs(), _ => SelectedJobs != null && SelectedJobs.Count > 0);
            RemoveJobCommand = new RelayCommand(_ => RemoveSelectedJob(), _ => SelectedJobs != null && SelectedJobs.Count > 0);
            PauseJobCommand = new RelayCommand(_ => PauseSelectedJob(), _ => CanPauseSelectedJob());
            ResumeJobCommand = new RelayCommand(_ => ResumeSelectedJob(), _ => CanResumeSelectedJob());
            StopJobCommand = new RelayCommand(_ => StopSelectedJobs(), _ => SelectedJobs != null && SelectedJobs.Any(job => job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused) && !_isLaunchingJobs);

            // Configure timer to refresh job status every second
            _statusUpdateTimer = new DispatcherTimer();
            _statusUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();
        }

        /// <summary>
        /// Timer callback to refresh job status information
        /// </summary>
        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (!_isLaunchingJobs)
                CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Loads all jobs from the BackupManager and updates the observable collection
        /// </summary>
        public void LoadJobs()
        {
            // Save currently selected job IDs to restore selection after refresh
            var selectedJobIds = SelectedJobs?.Select(j => j.Name).ToList() ?? new List<string>();

            // Ensure BackupManager has the latest data from JSON
            _backupManager.LoadJobs();

            // Get the updated job list
            var jobs = _backupManager.GetAllJobs();

            // Clear and reload jobs
            Jobs.Clear();
            foreach (var job in jobs)
            {
                // Restore selection state
                job.IsSelected = selectedJobIds.Contains(job.Name);
                Jobs.Add(job);

                // Ensure we update selection in the view model too
                if (job.IsSelected)
                {
                    if (SelectedJobs == null)
                        SelectedJobs = new List<BackupJob>();

                    if (!SelectedJobs.Contains(job))
                        SelectedJobs.Add(job);
                }
            }

            OnPropertyChanged(nameof(Jobs));
            OnPropertyChanged(nameof(SelectedJobs));
        }

        /// <summary>
        /// Updates the selection based on checkbox states
        /// </summary>
        public void UpdateSelectionFromCheckboxes()
        {
            var selected = Jobs.Where(j => j.IsSelected).ToList();
            SelectedJobs = selected;
            //SelectedJobs = selected.Count > 0 ? selected : null;
        }

        /// <summary>
        /// Adds a job to the observable collection and sets up property change notifications
        /// </summary>
        /// <param name="job">The job to add</param>
        public void JobAdded(BackupJob job)
        {
            if (!Jobs.Contains(job))
            {
                /*
                // Listen for property changes on the job's status
                if (job.Status is INotifyPropertyChanged statusNotifier)
                {
                    statusNotifier.PropertyChanged += (s, e) => 
                    {
                        // Force UI refresh when job status changes
                        OnPropertyChanged(nameof(Jobs));
                    };
                }
                */
                
                Jobs.Add(job);
                OnPropertyChanged(nameof(Jobs));
            }
        }

        /// <summary>
        /// Opens the job creation panel
        /// </summary>
        private void CreateJob()
        {
            Debug.WriteLine("MainViewModel.CreateJob() called.");
        }

        /// <summary>
        /// Launches the selected backup job
        /// </summary>
        private async Task LaunchSelectedJob()
        {
            // V�rifie si un lancement est d�j� en cours.
            if (_isLaunchingJobs)
            {
                Debug.WriteLine("LaunchSelectedJob: Un lancement est d�j� en cours. Annulation de la nouvelle requ�te de lancement."); // Adaptation de votre message de d�bogage.
                return;
            }

            // V�rifie si des travaux sont s�lectionn�s.
            if (SelectedJobs == null || !SelectedJobs.Any())
            {
                Debug.WriteLine("LaunchSelectedJob: Aucun travail s�lectionn�."); // Adaptation de votre message de d�bogage.
                return;
            }

            // Filtre les travaux qui peuvent �tre lanc�s (�tat Initialise, Erreur, ou Termin�).
            var jobsToStartModels = SelectedJobs.Where(j =>
                j.Status.State == BackupState.Initialise ||
                j.Status.State == BackupState.Error ||
                j.Status.State == BackupState.Completed).ToList();

            // V�rifie s'il y a des travaux �ligibles au lancement.
            if (!jobsToStartModels.Any())
            {
                Debug.WriteLine("LaunchSelectedJob: Aucun travail appropri� � d�marrer en fonction de la s�lection et de l'�tat actuels."); // Adaptation de votre message de d�bogage.
                CommandManager.InvalidateRequerySuggested(); // Met � jour l'�tat des boutons.
                return;
            }

            _isLaunchingJobs = true; // D�finit l'indicateur pour montrer qu'un lancement est en cours.
            CommandManager.InvalidateRequerySuggested(); // D�sactive les boutons de lancement.
            Debug.WriteLine($"MainViewModel.LaunchSelectedJob: D�FINI _isLaunchingJobs = true. Lancement des travaux : {string.Join(", ", jobsToStartModels.Select(j => j.Name))}"); // Adaptation de votre message de d�bogage.

            try
            {
                List<string> jobNamesToRun = new List<string>();
                foreach (var jobModel in jobsToStartModels)
                {
                    jobModel.Status.ResetForRun(); // R�initialise le statut du travail pour une nouvelle ex�cution.
                    Debug.WriteLine($"MainViewModel.LaunchSelectedJob: Statut du travail '{jobModel.Name}' R�INITIALIS� pour l'ex�cution. Nouveau statut : {jobModel.Status.State}"); // Adaptation de votre message de d�bogage.
                    jobNamesToRun.Add(jobModel.Name); // Ajoute le nom du travail � la liste des travaux � ex�cuter.
                }

                if (jobNamesToRun.Any())
                {
                    await _backupManager.ExecuteJobsByNameAsync(jobNamesToRun);
                    Debug.WriteLine("MainViewModel.LaunchSelectedJob: BackupManager.ExecuteJobsByNameAsync attendu et termin�."); // Adaptation de votre message de d�bogage.
                }
                else
                {
                    Debug.WriteLine("MainViewModel.LaunchSelectedJob: Aucun nom de travail valide trouv� � ex�cuter."); // Adaptation de votre message de d�bogage.
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainViewModel.LaunchSelectedJob: EXCEPTION lors de l'ex�cution du travail : {ex.Message}"); // Adaptation de votre message de d�bogage.
                                                                                                                              // G�rez l'exception (par exemple, notifier l'utilisateur).
                                                                                                                              // Vous pourriez vouloir afficher un message � l'utilisateur ici.
            }
            finally
            {
                _isLaunchingJobs = false; // R�initialise l'indicateur une fois tous les travaux termin�s ou en cas d'erreur.
                Debug.WriteLine("MainViewModel.LaunchSelectedJob: (dans finally) D�FINI _isLaunchingJobs = false."); // Adaptation de votre message de d�bogage.
                CommandManager.InvalidateRequerySuggested(); // R�active les boutons de lancement.
            }
        }

        /// <summary>
        /// Pauses the selected backup job
        /// </summary>
        private void PauseSelectedJob()
        {
            if (SelectedJobs != null)
                foreach (var job in SelectedJobs)
                {
                    if (job.Status.State == BackupState.Running)
                    {
                        job.Pause();
                    }
                }
        }

        /// <summary>
        /// Determines if the selected job can be paused
        /// </summary>
        private bool CanPauseSelectedJob()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
                return false;
            foreach (var job in SelectedJobs)
            {
                if (job.Status.State != BackupState.Running)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Resumes the selected backup job
        /// </summary>
        private void ResumeSelectedJob()
        {
            if (SelectedJobs != null)
                foreach (var job in SelectedJobs)
                {
                    if (job.Status.State == BackupState.Paused)
                    {
                        job.Resume();
                    }
                }
        }

        /// <summary>
        /// Determines if the selected job can be resumed
        /// </summary>
        private bool CanResumeSelectedJob()
        {
            if (SelectedJobs == null || SelectedJobs.Count == 0)
                return false;
            foreach (var job in SelectedJobs)
            {
                if (job.Status.State != BackupState.Paused)
                    return false;
            }
            return true;
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
                _backupManager.ExecuteJobsAsync(jobIndicesRef);
            }
        }

        /// <summary>
        /// Removes the selected backup job
        /// </summary>
        
        private void RemoveSelectedJob()
        {
            var jobsToRemove = new List<BackupJob>(SelectedJobs);

            foreach (var job in jobsToRemove)
            {
                if (_backupManager.RemoveJobByName(job.Name))
                {
                    Jobs.Remove(job); 
                }
            }
            UpdateSelectionFromCheckboxes();
            OnPropertyChanged(nameof(Jobs));
        }

        private void StopSelectedJobs()
        {
            if (SelectedJobs == null || _isLaunchingJobs) return; // Do not allow stopping if a launch is in progress (could be adjusted)
            Debug.WriteLine($"MainViewModel.StopSelectedJobs called for: {string.Join(", ", SelectedJobs.Where(j => j.Status.State == BackupState.Running || j.Status.State == BackupState.Paused).Select(j => j.Name))}");
            foreach (var job in SelectedJobs)
            {
                if (job.Status.State == BackupState.Running || job.Status.State == BackupState.Paused) job.Stop();
            }
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Gets a formatted string representation of job state
        /// </summary>
        public string GetJobStateDisplay(BackupJob job)
        {
            if (job == null)
                return string.Empty;

            return job.Status.State switch
            {
                BackupState.Waiting => "En attente",
                BackupState.Running => "En cours",
                BackupState.Paused => "En pause",
                BackupState.Completed => "Termin�",
                BackupState.Error => "Erreur",
                _ => "Inconnu"
            };
        }


        /// <summary>
        /// Gets the job progress as a string with percentage
        /// </summary>
        public string GetJobProgressDisplay(BackupJob job)
        {
            if (job == null)
                return "0%";

            return $"{job.Status.ProgressPercentage:F1}%";
        }

        /// <summary>
        /// Gets the job's remaining time estimation as a formatted string
        /// </summary>
        public string GetRemainingTimeDisplay(BackupJob job)
        {
            if (job == null || job.Status.State != BackupState.Running)
                return string.Empty;

            var remaining = job.Status.EstimatedTimeRemaining;

            if (remaining.TotalSeconds < 1)
                return "Calcul en cours...";

            if (remaining.TotalHours >= 1)
                return $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m {remaining.Seconds:D2}s";
            else if (remaining.TotalMinutes >= 1)
                return $"{remaining.Minutes}m {remaining.Seconds:D2}s";
            else
                return $"{remaining.Seconds}s";
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