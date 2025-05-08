using EasySave.Models;

namespace EasySave.Interfaces
{
    public interface IBackupObserver
    {
        void Update(BackupJob job, BackupStatus status);
    }
}