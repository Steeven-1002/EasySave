using System;
using System.Collections.Generic;
using EasySave.Interfaces;
using EasySave.Services;

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
            UpdateState();
        }

        public void Execute()
        {
            Console.WriteLine($"BackupJob '{Name}': Preparing to execute via strategy '{Strategy.GetType().Name}'.");
            this.State = BackupState.ACTIVE;
            Strategy.RegisterObserver(Services.LoggingBackup.Instance);
            Strategy.RegisterStateObserver(Services.StateManager.Instance);
            Strategy.Execute(this);
            this.LastRunTime = DateTime.Now;
        }

        public List<string> GetFilesToBackup()
        {
            return Strategy.GetFilesToBackup(this);
        }

        public void UpdateState()
        {
            StateManager.Instance.LoadState();
            var jobState = StateManager.Instance.GetState(Name);
            if (jobState != null)
            {
                State = jobState.State;
            }
        }

        public override string ToString()
        {
            return $"Job: {Name}, Type: {Type}, Source: {SourcePath}, Target: {TargetPath}, State: {State}, LastRun: {LastRunTime}";
        }
    }
}