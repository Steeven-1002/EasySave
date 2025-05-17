using System;

namespace EasySave_by_ProSoft.Models {
	public class BackupManager {
		private List<BackupJob> backupJobs;
		private string jobsConfigFilePath;

		public BackupJob AddJob(string name, ref string sourcePath, ref string targetPath, ref BackupType type) {
			throw new System.NotImplementedException("Not implemented");
		}
		public bool RemoveJob(ref int jobIndex) {
			throw new System.NotImplementedException("Not implemented");
		}
		public BackupJob GetJob(ref int jobIndex) {
			throw new System.NotImplementedException("Not implemented");
		}
		public List<BackupJob> GetAllJobs() {
			throw new System.NotImplementedException("Not implemented");
		}
		public void ExecuteJobs(ref List<int> jobIndexes) {
			throw new System.NotImplementedException("Not implemented");
		}
		public void LoadJobs() {
			throw new System.NotImplementedException("Not implemented");
		}

		private BackupJob backupJob;

	}

}
