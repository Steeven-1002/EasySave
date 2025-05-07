namespace EasySave
{
    public interface IBackupObserver
    {
        void UpdateJobState(BackupJob job, BackupStatus status); // Note: BackupStatus est un placeholder
    }
}