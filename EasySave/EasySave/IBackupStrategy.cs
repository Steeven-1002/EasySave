using System.Collections.Generic;

namespace EasySave
{
    public interface IBackupStrategy
    {
        List<string> ExecuteBackup(BackupJob job);
        List<string> GetFilesToBackup(BackupJob job);
    }
}