using System.Collections.Generic;
using EasySave.Models;
using EasySave.Services;

namespace EasySave.Interfaces
{
    public interface IBackupStrategy
    {
        void Execute(BackupJob job);
        void RegisterObserver(IBackupObserver observer);
        void RegisterStateObserver(IStateObserver observer);
        List<string> GetFilesToBackup(BackupJob job);
    }
}