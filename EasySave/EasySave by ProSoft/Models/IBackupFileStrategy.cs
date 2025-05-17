using System;

namespace EasySave_by_ProSoft.Models {
	public interface IBackupFileStrategy {
		void GetFiles(ref BackupJob job);

	}

}
