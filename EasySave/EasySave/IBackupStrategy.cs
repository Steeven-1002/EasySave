using System.Collections.Generic;
using EasySave.Models;

namespace EasySave.Interfaces
{
    public interface IBackupStrategy
    {
        void Execute(BackupJob job);
        void RegisterObserver(IBackupObserver observer);
        List<string> GetFilesToBackup(BackupJob job);
    }
}