using System;

namespace EasySave_by_ProSoft.Models {
    public interface JobEventListeners {
        void Update(string jobName, BackupState newState, int totalFiles, long totalSize, int remainingFiles, long remainingSize, string currentSourceFile, string currentTargetFile, double transfertDuration);
        }
}
