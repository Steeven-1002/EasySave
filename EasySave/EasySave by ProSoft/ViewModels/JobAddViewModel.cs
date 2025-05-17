using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EasySave_by_ProSoft.Models;

namespace EasySave_by_ProSoft.ViewModels
{
    public class JobAddViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _sourcePath = string.Empty;
        private string _targetPath = string.Empty;
        private BackupType _type = BackupType.Full;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); }
        }

        public string TargetPath
        {
            get => _targetPath;
            set { _targetPath = value; OnPropertyChanged(); }
        }

        public BackupType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public ICommand AddJobCommand { get; }

        public event Action<BackupJob>? JobAdded;

        public JobAddViewModel()
        {
            AddJobCommand = new RelayCommand(_ => AddJob(), _ => CanAddJob());
        }

        private void AddJob()
        {
            var job = new BackupJob(Name, SourcePath, TargetPath, Type, null!);
            JobAdded?.Invoke(job);
            // Optionally reset fields after add
            Name = string.Empty;
            SourcePath = string.Empty;
            TargetPath = string.Empty;
            Type = BackupType.Full;
        }

        private bool CanAddJob()
        {
            return !string.IsNullOrWhiteSpace(Name)
                && !string.IsNullOrWhiteSpace(SourcePath)
                && !string.IsNullOrWhiteSpace(TargetPath);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}