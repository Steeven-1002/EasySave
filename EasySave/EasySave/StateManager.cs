using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace EasySave
{
    public class StateManager
    {
        private string _stateFilePath;
        public List<JobState> JobStates { get; set; }
        private List<IStateObserver> _observers = new List<IStateObserver>(); // Pour le pattern Observer

        public StateManager(string stateFilePath)
        {
            _stateFilePath = stateFilePath;
            JobStates = new List<JobState>();
            LoadState(); // Charge l'état au démarrage
        }

        public void RegisterObserver(IStateObserver observer)
        {
            _observers.Add(observer);
        }

        public void UnregisterObserver(IStateObserver observer)
        {
            _observers.Remove(observer);
        }

        private void NotifyObservers(string jobName, BackupState newState)
        {
            foreach (var observer in _observers)
            {
                observer.OnStateChange(jobName, newState);
            }
        }

        public void UpdateJobState(string jobName, BackupState newState, DateTime lastTimeState)
        {
            var jobState = JobStates.FirstOrDefault(js => js.JobName == jobName);
            if (jobState != null)
            {
                jobState.State = newState;
                jobState.LastTimeState = lastTimeState;
                NotifyObservers(jobName, newState); // Notifie les observateurs
                SaveState();
            }
        }

        public JobState GetJobState(string jobName)
        {
            return JobStates.FirstOrDefault(js => js.JobName == jobName);
        }

        public void SaveState()
        {
            try
            {
                string json = JsonSerializer.Serialize(JobStates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde de l'état : {ex.Message}");
            }
        }

        public void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    string json = File.ReadAllText(_stateFilePath);
                    JobStates = JsonSerializer.Deserialize<List<JobState>>(json);
                }
                else
                {
                    JobStates = new List<JobState>(); // Initialise avec une liste vide si le fichier n'existe pas
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement de l'état : {ex.Message}");
                JobStates = new List<JobState>(); // Assure une liste vide en cas d'erreur
            }
        }
    }

    public class JobState
    {
        public string JobName { get; set; }
        public BackupState State { get; set; }
        public DateTime LastTimeState { get; set; }
        // remaining files, etc.
    }

    public interface IStateObserver
    {
        void OnStateChange(string jobName, BackupState newState);
    }
}