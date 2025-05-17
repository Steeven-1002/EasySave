using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySave_by_ProSoft.Models {
    /// <summary>
    /// Provides comprehensive job tracking capabilities throughout the backup process lifecycle
    /// </summary>
    public class JobStatus : INotifyPropertyChanged {
        private long totalSize;
        public long TotalSize {
            get {
                return totalSize;
            }
            set {
                if (totalSize != value) {
                    totalSize = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private int totalFiles;
        public int TotalFiles {
            get {
                return totalFiles;
            }
            set {
                if (totalFiles != value) {
                    totalFiles = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private BackupState state;
        public BackupState State {
            get {
                return state;
            }
            set {
                if (state != value) {
                    state = value;
                    LastStateChangeTime = DateTime.Now;
                    OnPropertyChanged();
                }
            }
        }
        
        private long remainingSize;
        public long RemainingSize {
            get {
                return remainingSize;
            }
            set {
                if (remainingSize != value) {
                    remainingSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(TransferredSize));
                }
            }
        }
        
        private int remainingFiles;
        public int RemainingFiles {
            get {
                return remainingFiles;
            }
            set {
                if (remainingFiles != value) {
                    remainingFiles = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProcessedFiles));
                }
            }
        }
        
        private string currentTargetFIle;
        public string CurrentTargetFIle {
            get {
                return currentTargetFIle;
            }
            set {
                if (currentTargetFIle != value) {
                    currentTargetFIle = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private string currentSourceFile;
        public string CurrentSourceFile {
            get {
                return currentSourceFile;
            }
            set {
                if (currentSourceFile != value) {
                    currentSourceFile = value;
                    OnPropertyChanged();
                }
            }
        }
        
        // Nouvelles propri�t�s pour le suivi du job
        
        /// <summary>
        /// Points de restauration pour la reprise de la sauvegarde
        /// </summary>
        private List<string> processedFiles = new List<string>();
        public List<string> ProcessedFiles => processedFiles;
        
        /// <summary>
        /// Heure de d�but de la t�che
        /// </summary>
        public DateTime StartTime { get; private set; }
        
        /// <summary>
        /// Heure de fin de la t�che
        /// </summary>
        public DateTime? EndTime { get; private set; }
        
        /// <summary>
        /// Temps �coul� depuis le d�but
        /// </summary>
        public TimeSpan ElapsedTime => (EndTime ?? DateTime.Now) - StartTime;
        
        /// <summary>
        /// Derni�re mise � jour de l'�tat
        /// </summary>
        public DateTime LastStateChangeTime { get; private set; }
        
        /// <summary>
        /// Identifiant unique de l'ex�cution de la t�che
        /// </summary>
        public Guid ExecutionId { get; private set; }
        
        /// <summary>
        /// Taille totale des donn�es transf�r�es
        /// </summary>
        public long TransferredSize => TotalSize - RemainingSize;
        
        /// <summary>
        /// Pourcentage de progression
        /// </summary>
        public double ProgressPercentage => TotalSize > 0 ? Math.Round((double)(TotalSize - RemainingSize) / TotalSize * 100, 2) : 0;
        
        /// <summary>
        /// Vitesse de transfert en octets par seconde
        /// </summary>
        public double TransferRate => ElapsedTime.TotalSeconds > 0 ? TransferredSize / ElapsedTime.TotalSeconds : 0;
        
        /// <summary>
        /// Temps restant estim�
        /// </summary>
        public TimeSpan EstimatedTimeRemaining {
            get {
                if (TransferRate > 0 && RemainingSize > 0)
                    return TimeSpan.FromSeconds(RemainingSize / TransferRate);
                return TimeSpan.Zero;
            }
        }
        
        /// <summary>
        /// Messages d'erreur si applicables
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Gestionnaire d'�v�nements pour notifier les observateurs
        /// </summary>
        public JobEventManager Events;
        
        private BackupState backupState;
        private JobEventManager jobEventManager;
        private BackupJob backupJob;
        
        /// <summary>
        /// Constructeur de JobStatus
        /// </summary>
        public JobStatus() {
            Events = new JobEventManager();
            ExecutionId = Guid.NewGuid();
            StartTime = DateTime.Now;
            State = BackupState.Waiting;
            LastStateChangeTime = StartTime;
        }
        
        /// <summary>
        /// Met � jour le statut du job et notifie les observateurs
        /// </summary>
        public void Update() {
            // Notifier les observateurs des changements
            if (Events != null) {
                Events.NotifyListeners(ref this);
            }
        }
        
        /// <summary>
        /// Marque un fichier comme trait� pour permettre la reprise
        /// </summary>
        /// <param name="filePath">Chemin du fichier trait�</param>
        public void AddProcessedFile(string filePath) {
            if (!processedFiles.Contains(filePath)) {
                processedFiles.Add(filePath);
            }
        }
        
        /// <summary>
        /// Indique que la t�che a d�marr�
        /// </summary>
        public void Start() {
            StartTime = DateTime.Now;
            State = BackupState.Running;
            EndTime = null;
            Update();
        }
        
        /// <summary>
        /// Indique que la t�che est en pause
        /// </summary>
        public void Pause() {
            State = BackupState.Paused;
            Update();
        }
        
        /// <summary>
        /// Indique que la t�che est termin�e
        /// </summary>
        public void Complete() {
            State = BackupState.Completed;
            EndTime = DateTime.Now;
            RemainingFiles = 0;
            RemainingSize = 0;
            Update();
        }
        
        /// <summary>
        /// Indique que la t�che a rencontr� une erreur
        /// </summary>
        /// <param name="errorMessage">Message d'erreur</param>
        public void SetError(string errorMessage) {
            State = BackupState.Error;
            ErrorMessage = errorMessage;
            EndTime = DateTime.Now;
            Update();
        }
        
        /// <summary>
        /// Cr�e un instantan� de l'�tat actuel pour la persistance ou l'affichage
        /// </summary>
        /// <returns>�tat du job pour s�rialisation</returns>
        public JobState CreateSnapshot() {
            var snapshot = new JobState {
                JobName = backupJob?.Name ?? string.Empty,
                Timestamp = DateTime.Now,
                State = this.State,
                TotalFiles = this.TotalFiles,
                TotalSize = this.TotalSize,
                CurrentSourceFile = this.CurrentSourceFile,
                CurrentTargetFile = this.CurrentTargetFIle
            };
            
            return snapshot;
        }
        
        /// <summary>
        /// Reprend l'ex�cution d'une t�che apr�s une pause ou un arr�t
        /// </summary>
        public void Resume() {
            if (State == BackupState.Paused) {
                State = BackupState.Running;
                Update();
            }
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
