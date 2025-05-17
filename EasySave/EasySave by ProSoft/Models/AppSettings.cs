using System;

namespace EasySave_by_ProSoft.Models {
	public class AppSettings {
		private AppSettings instance;
		private string configFilePath;
		private Dictionary settings;

		public EasySave.AppSettings GetInstance() {
			return this.instance;
		}
		public void LoadConfiguration() {
			throw new System.NotImplementedException("Not implemented");
		}
		public void SaveConfiguration() {
			throw new System.NotImplementedException("Not implemented");
		}
		public Object GetSetting(ref string key) {
			throw new System.NotImplementedException("Not implemented");
		}
		public void SetSetting(ref string key, ref Object value) {
			throw new System.NotImplementedException("Not implemented");
		}

		private AppSettings appSettings2;

		private AppSettings appSettings;

	}

}
