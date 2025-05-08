using System;
using System.Text.Json;

namespace EasySave.Models
{
    public class JobState
    {
        public string JobName { get; set; }
        public DateTime Timestamp { get; set; }
        public BackupState State { get; set; }
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public int RemainingFiles { get; set; }
        public long RemainingSize { get; set; }
        public string CurrentSourceFile { get; set; }
        public string CurrentTargetFile { get; set; }

        public JobState(string jobName)
        {
            JobName = jobName;
            Timestamp = DateTime.Now;
            State = BackupState.INACTIVE;
            CurrentSourceFile = string.Empty;
            CurrentTargetFile = string.Empty;
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}