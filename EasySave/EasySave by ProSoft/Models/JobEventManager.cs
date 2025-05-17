using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasySave_by_ProSoft.Models 
{
    /// <summary>
    /// Implémente le design pattern Observer qui est chargé de communiquer les changements d'état
    /// des jobs aux différents listeners (logs, fichier state.json, vue MVVM)
    /// </summary>
    public class JobEventManager 
    {
        // Liste des différents listeners qui observent les changements d'état des jobs
        private List<JobEventListeners> listeners = new List<JobEventListeners>();
        
        // Chemin du fichier state.json maintenu en temps réel
        private readonly string stateFilePath;

        /// <summary>
        /// Initialise une nouvelle instance du gestionnaire d'événements des jobs
        /// </summary>
        public JobEventManager() 
        {
            // Définir l'emplacement du fichier state.json
            stateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EasySave",
                "state.json"
            );
            
            // Créer le répertoire s'il n'existe pas
            string directoryPath = Path.GetDirectoryName(stateFilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        /// <summary>
        /// Ajoute un listener qui sera notifié lors des changements d'état des jobs
        /// </summary>
        /// <param name="listener">Le listener à ajouter</param>
        public void AddListener(JobEventListeners listener) 
        {
            if (listener != null && !listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        /// <summary>
        /// Supprime un listener de la liste des observateurs
        /// </summary>
        /// <param name="listener">Le listener à supprimer</param>
        public void RemoveListener(JobEventListeners listener) 
        {
            listeners.Remove(listener);
        }

        /// <summary>
        /// Notifie tous les listeners d'un changement d'état du job et met à jour le fichier state.json
        /// </summary>
        /// <param name="jobStatus">Le statut du job qui a été modifié</param>
        public void NotifyListeners(ref JobStatus jobStatus)
        {
            // Mise à jour du fichier state.json en temps réel
            UpdateStateFile(jobStatus);

            // Notification de tous les listeners (logs, interfaces, etc.)
            foreach (var listener in listeners)
            {
                listener.Update(
                    jobStatus.ExecutionId.ToString(),
                    jobStatus.State,
                    jobStatus.TotalFiles,
                    jobStatus.TotalSize,
                    jobStatus.RemainingFiles,
                    jobStatus.RemainingSize,
                    jobStatus.CurrentSourceFile,
                    jobStatus.CurrentTargetFIle,
                    jobStatus.TransferRate
                );
            }
        }
        
        /// <summary>
        /// Met à jour le fichier state.json avec l'état actuel du job
        /// </summary>
        /// <param name="jobStatus">Le statut du job à enregistrer</param>
        private void UpdateStateFile(JobStatus jobStatus)
        {
            try
            {
                // Créer un snapshot du job pour la sérialisation
                var snapshot = jobStatus.CreateSnapshot();
                
                // Charger les états existants, s'il y en a
                List<JobState> allJobStates = new List<JobState>();
                if (File.Exists(stateFilePath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(stateFilePath);
                        if (!string.IsNullOrEmpty(jsonContent))
                        {
                            allJobStates = JsonSerializer.Deserialize<List<JobState>>(jsonContent) ?? new List<JobState>();
                        }
                    }
                    catch (JsonException)
                    {
                        // En cas d'erreur de désérialisation, on repart avec une liste vide
                        allJobStates = new List<JobState>();
                    }
                }
                
                // Mettre à jour l'état du job actuel ou l'ajouter s'il n'existe pas
                bool jobFound = false;
                for (int i = 0; i < allJobStates.Count; i++)
                {
                    if (allJobStates[i].JobName == snapshot.JobName)
                    {
                        allJobStates[i] = snapshot;
                        jobFound = true;
                        break;
                    }
                }
                
                if (!jobFound)
                {
                    allJobStates.Add(snapshot);
                }
                
                // Options de sérialisation pour avoir un JSON lisible
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                
                // Sérialiser et enregistrer dans le fichier
                string serializedData = JsonSerializer.Serialize(allJobStates, options);
                File.WriteAllText(stateFilePath, serializedData.Replace("},", "},\n")); // Add newlines for readability
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la mise à jour du fichier state.json: {ex.Message}");
                // Ne pas propager l'erreur pour éviter de perturber le flux principal
            }
        }
        
        /// <summary>
        /// Récupère les états de tous les jobs à partir du fichier state.json
        /// </summary>
        /// <returns>Liste des états des jobs</returns>
        public List<JobState> GetAllJobStates()
        {
            try
            {
                if (File.Exists(stateFilePath))
                {
                    string jsonContent = File.ReadAllText(stateFilePath);
                    return JsonSerializer.Deserialize<List<JobState>>(jsonContent) ?? new List<JobState>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la lecture du fichier state.json: {ex.Message}");
            }
            
            return new List<JobState>();
        }
        
        /// <summary>
        /// Nettoie le fichier state.json en supprimant les états des jobs terminés ou en erreur
        /// </summary>
        public void CleanupCompletedJobs()
        {
            try
            {
                List<JobState> states = GetAllJobStates();
                List<JobState> activeStates = states.FindAll(s => 
                    s.State != BackupState.Completed && s.State != BackupState.Error);
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                
                string serializedData = JsonSerializer.Serialize(activeStates, options);
                File.WriteAllText(stateFilePath, serializedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du nettoyage du fichier state.json: {ex.Message}");
            }
        }
    }
}
