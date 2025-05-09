using System;
using System.Collections.Generic;
using EasySave.Interfaces; // Pour IBackupStrategy

namespace EasySave.Models
{
    public class BackupJob
    {
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public BackupType Type { get; set; }
        public BackupState State { get; set; }
        public DateTime LastRunTime { get; set; }
        public DateTime CreationTime { get; set; }
        public IBackupStrategy Strategy { get; set; }

        public BackupJob(string name, string sourcePath, string targetPath, BackupType type, IBackupStrategy strategy)
        {
            Name = name;
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Type = type;
            Strategy = strategy;
            State = BackupState.INACTIVE;
            CreationTime = DateTime.Now;
            LastRunTime = DateTime.MinValue;
        }

        public void Execute()
        {
            // La logique d'exécution est déléguée à la stratégie.
            // Le BackupManager ou une classe d'orchestration appellera ceci.
            // L'état du job (this.State) sera mis à jour par la stratégie ou
            // par le StateManager via des notifications d'observateur.
            Console.WriteLine($"BackupJob '{Name}': Preparing to execute via strategy '{Strategy.GetType().Name}'.");
            this.State = BackupState.ACTIVE; // État initial avant l'appel à la stratégie
            Strategy.RegisterObserver(Services.LoggingBackup.Instance); // Enregistrement de l'observateur
            Strategy.RegisterStateObserver(Services.StateManager.Instance);
            Strategy.Execute(this);
            // Après l'exécution de la stratégie, l'état final (COMPLETED, ERROR)
            // sera mis à jour (potentiellement par la stratégie elle-même ou par le gestionnaire d'état).
            this.LastRunTime = DateTime.Now;
        }

        public List<string> GetFilesToBackup()
        {
            return Strategy.GetFilesToBackup(this);
        }

        public long GetTotalSize()
        {
            long totalSize = 0;
            return totalSize;
        }

        public override string ToString()
        {
            return $"Job: {Name}, Type: {Type}, Source: {SourcePath}, Target: {TargetPath}, State: {State}, LastRun: {LastRunTime}";
        }
    }
}