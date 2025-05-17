using System;

namespace EasySave_by_ProSoft.Models {
	public class BackupJob {
		public string Name;
		public string SourcePath;
		public string TargetPath;
		public BackupType Type;
		public JobStatus Status;

		public void Start() {
			throw new System.NotImplementedException("Not implemented");
		}
		public void Pause() {
			throw new System.NotImplementedException("Not implemented");
		}
		public void Stop() {
			throw new System.NotImplementedException("Not implemented");
		}
		public JobStatus GetStatus() {
			return this.Status;
		}

		private BackupType backupType;
		private JobStatus jobStatus;
		private IBackupFileStrategy iBackupFileStrategy;

		private BackupManager backupManager;

	}

}
