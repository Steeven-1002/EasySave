using System;

namespace EasySave_by_ProSoft.Models {
	public class JobState {
		private string stateFilePath;
		public string JobName;
		public DateTime Timestamp;
		public BackupState State;
		public int TotalFiles;
		public long TotalSize;
		public string CurrentSourceFile;
		public string CurrentTargetFile;

		public void Update(ref JobStatus jobStatus) {
			throw new System.NotImplementedException("Not implemented");
		}
		public void SaveState() {
			throw new System.NotImplementedException("Not implemented");
		}
		public void GetState() {
			throw new System.NotImplementedException("Not implemented");
		}

	}

}
