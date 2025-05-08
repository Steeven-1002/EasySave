using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EasySave.Models;
using EasySave.Interfaces;

namespace EasySave.Services
{
    public class StateManager
    {
        private readonly string _stateFilePath;
        private List<JobState> _jobStates;
        private List<IStateObserver> _observers;

        public StateManager(string stateFilePath)
        {
            _stateFilePath = stateFilePath;
            _jobStates = new List<JobState>();
            _observers = new List<IStateObserver>();
            LoadState(); // Charger l'état existant à l'initialisation
        }

        // Le diagramme indique : UpdateJobState(job: BackupJob, currentFile: string, targetFile: string, remainingFiles: int, remainingSize: long)
        // Je vais adapter pour utiliser JobState directement ou le nom du job
        public void UpdateJobState(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile)
        {
            JobState? jobState = _jobStates.FirstOrDefault(js => js.JobName == jobName);
            bool newEntry = false;
            if (jobState == null)
            {
                jobState = new JobState(jobName);
                newEntry = true;
            }

            jobState.Timestamp = DateTime.Now;
            jobState.State = newState;
            jobState.TotalFiles = totalFiles;
            jobState.TotalSize = totalSize;
            jobState.RemainingFiles = remainingFiles;
            jobState.RemainingSize = remainingSize;
            jobState.CurrentSourceFile = currentSourceFile;
            jobState.CurrentTargetFile = currentTargetFile;

            if (newEntry) _jobStates.Add(jobState); // Ajouter seulement si c'est une nouvelle entrée

            Console.WriteLine($"StateManager: Updated state for job '{jobName}'. Current file: {currentSourceFile}");
            NotifyObservers(jobState);
            SaveState();
        }

        public void InitializeJobState(string jobName, int totalFiles, long totalSize)
        {
            UpdateJobState(jobName, BackupState.ACTIVE, totalFiles, totalSize, totalFiles, totalSize, "Starting scan...", "");
        }

        public void FinalizeJobState(string jobName, BackupState finalState)
        {
            UpdateJobState(jobName, finalState, 0, 0, 0, 0, "Finalized", ""); // Les totaux pourraient être ceux de fin
        }


        public void SaveState()
        {
            try
            {
                string json = JsonSerializer.Serialize(_jobStates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StateManager ERROR saving state to '{_stateFilePath}': {ex.Message}");
            }
        }

        public void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    string json = File.ReadAllText(_stateFilePath);
                    _jobStates = JsonSerializer.Deserialize<List<JobState>>(json) ?? new List<JobState>();
                }
                else
                {
                    _jobStates = new List<JobState>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StateManager ERROR loading state from '{_stateFilePath}': {ex.Message}");
                _jobStates = new List<JobState>();
            }
        }

        public JobState? GetState(string jobName)
        {
            return _jobStates.FirstOrDefault(js => js.JobName == jobName);
        }

        public void RegisterObserver(IStateObserver observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }

        public void UnregisterObserver(IStateObserver observer) // Méthode utile
        {
            _observers.Remove(observer);
        }

        private void NotifyObservers(JobState state)
        {
            foreach (var observer in _observers)
            {
                observer.StateChanged(state);
            }
        }

        private void CreateStateFile() // Définie dans le diagramme
        {
            // Assure que le fichier est créé s'il n'existe pas, potentiellement avec un contenu vide ou initial.
            if (!File.Exists(_stateFilePath))
            {
                SaveState(); // Sauvegarde une liste vide de _jobStates ou l'état actuel.
            }
        }
    }
}