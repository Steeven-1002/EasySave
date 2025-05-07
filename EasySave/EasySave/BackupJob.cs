using System;
using System.Collections.Generic;

namespace EasySave
{
    public class BackupJob
    {
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public BackupType BackupType { get; set; }
        public BackupState BackupState { get; set; }
        public DateTime LastTimeState { get; set; }
        public DateTime LastTimeRun { get; set; }
        public DateTime CreationTime { get; set; }
        public BackupStrategy BackupStrategy { get; set; } // Stratégie de sauvegarde (Full ou Differential)

        public BackupJob()
        {
            // Initialise l'état par défaut
            BackupState = BackupState.INACTIVE;
        }

        public List<string> Execute()
        {
            // Exécute la sauvegarde en utilisant la stratégie définie
            if (BackupStrategy == null)
            {
                throw new InvalidOperationException("Aucune stratégie de sauvegarde définie pour cette tâche.");
            }

            LastTimeRun = DateTime.Now;
            return BackupStrategy.ExecuteBackup(this);
        }

        public string GetSetting(string key)
        {
            // Peut être utilisé pour récupérer des paramètres spécifiques à la tâche (si nécessaire)
            // Actuellement non implémenté
            return null;
        }

        public void SetSetting(string key, string value)
        {
            // Peut être utilisé pour définir des paramètres spécifiques à la tâche (si nécessaire)
            // Actuellement non implémenté
        }

        // ... (Autres méthodes si nécessaire) ...
    }
}