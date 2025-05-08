using EasySave.Models;

namespace EasySave.Interfaces
{
    public interface IBackupObserver
    {
        void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile);
    }
}