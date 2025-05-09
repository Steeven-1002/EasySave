using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EasySave.Models;
using EasySave.Interfaces;
using EasySave.ConsoleApp;

namespace EasySave.Services
{
    public class StateManager : IStateObserver // Implémentation de l'interface IStateObserver
    {
        private static StateManager? _instance;
        private readonly string _stateFilePath;
        private List<JobState> _jobStates;
        private readonly List<IStateObserver> _observers = new(); // Initialisation pour éviter CS8618

        public StateManager(string stateFilePath)
        {
            _stateFilePath = stateFilePath;
            _jobStates = new List<JobState>();
            LoadState(); // Charger l'état existant à l'initialisation
        }

        public static StateManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new StateManager(ReferenceEquals(ConfigManager.Instance.StateFilePath, "") ? "state.json" : ConfigManager.Instance.StateFilePath);
                }
                return _instance;
            }
        }

        public void StateChanged(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile)
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
            SaveState();
        }

        public void InitializeJobState(string jobName, int totalFiles, long totalSize)
        {
            StateChanged(jobName, BackupState.ACTIVE, totalFiles, totalSize, totalFiles, totalSize, "Starting scan...", "");
        }

        public void FinalizeJobState(string jobName, BackupState finalState)
        {
            StateChanged(jobName, finalState, 0, 0, 0, 0, "Finalized", ""); // Les totaux pourraient être ceux de fin
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

        private void CreateStateFile() // Définie dans le diagramme
        {
            if (!File.Exists(_stateFilePath))
            {
                SaveState(); // Sauvegarde une liste vide de _jobStates ou l'état actuel.
            }
        }
    }
}