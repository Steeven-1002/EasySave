using EasySave.Models;

namespace EasySave.Interfaces
{
    public interface IStateObserver
    {
        void StateChanged(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile);

    }
}