using System;

namespace EasySave
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

        public override string ToString()
        {
            return $"Job: {JobName}, State: {State}, Progress: {TotalFiles - RemainingFiles}/{TotalFiles} files, Size: {TotalSize - RemainingSize}/{TotalSize} bytes, Current: {CurrentSourceFile} -> {CurrentTargetFile}";
        }
    }
}