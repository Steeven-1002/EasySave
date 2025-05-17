using System;

namespace EasySave_by_ProSoft.Models {
	public class JobStatus {
		private long totalSize;
		public long TotalSize {
			get {
				return totalSize;
			}
			set {
				totalSize = value;
			}
		}
		private int totalFiles;
		public int TotalFiles {
			get {
				return totalFiles;
			}
			set {
				totalFiles = value;
			}
		}
		private BackupState state;
		public BackupState State {
			get {
				return state;
			}
			set {
				state = value;
			}
		}
		private long remainingSize;
		public long RemainingSize {
			get {
				return remainingSize;
			}
			set {
				remainingSize = value;
			}
		}
		private int remainingFiles;
		public int RemainingFiles {
			get {
				return remainingFiles;
			}
			set {
				remainingFiles = value;
			}
		}
		private string currentTargetFIle;
		public string CurrentTargetFIle {
			get {
				return currentTargetFIle;
			}
			set {
				currentTargetFIle = value;
			}
		}
		private string currentSourceFile;
		public string CurrentSourceFile {
			get {
				return currentSourceFile;
			}
			set {
				currentSourceFile = value;
			}
		}
		public JobEventManager Events;

		public void Update() {
			throw new System.NotImplementedException("Not implemented");
		}

		private BackupState backupState;
		private JobEventManager jobEventManager;

		private BackupJob backupJob;

	}

}
