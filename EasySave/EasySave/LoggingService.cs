using System;
// Les interfaces IBackupObserver et IStateObserver doivent être définies
// dans EasySave.Interfaces également.
using EasySave.Models;

namespace EasySave.Interfaces
{
    // Cette interface représente le contrat que la DLL externe doit implémenter.
    // Elle hérite aussi des interfaces d'observateur comme indiqué dans le diagramme.
    public interface LoggingService : IBackupObserver, IStateObserver
    {
        void WriteLog(DateTime timestamp, string jobName, string sourcePath, string targetPath, long fileSize, long transferTime);
        string GetLogFilePath();
    }
}