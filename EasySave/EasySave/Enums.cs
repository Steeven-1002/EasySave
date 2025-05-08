using EasySave.Models;

namespace EasySave
{
    // Enum pour les types de sauvegarde (à déplacer si nécessaire)
    public enum BackupType { 
        FULL = 0,
        DIFFERENTIAL = 1
    }

    // Enum pour les états des tâches (à déplacer si nécessaire)
    public class JobState
    {
        public string JobName { get; set; }
        public DateTime Timestamp { get; set; }
        public BackupState State { get; set; }
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public int RemainingFiles { get; set; }
        public long RemainingSize { get; set; }
        public string CurrentSourceFile { get; set; }
        public string CurrentTargetFile { get; set; }

        // Constructor accepting jobName  
        public JobState(string jobName)
        {
            JobName = jobName;
            Timestamp = DateTime.Now;
            State = BackupState.INACTIVE;
            TotalFiles = 0;
            TotalSize = 0;
            RemainingFiles = 0;
            RemainingSize = 0;
            CurrentSourceFile = string.Empty;
            CurrentTargetFile = string.Empty;
        }
    }
}