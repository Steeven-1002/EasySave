using System;
using System.Collections.Generic;

namespace EasySave_by_ProSoft.Models {
    public class JobEventManager {
        private List<JobEventListeners> listeners = new List<JobEventListeners>();

        public void AddListener(JobEventListeners listener) {
            listeners.Add(listener);
        }

        public void RemoveListener(JobEventListeners listener) {
            listeners.Remove(listener);
        }

        public void NotifyListeners(ref JobStatus jobStatus) {
            foreach (var listener in listeners) {
                listener.Update(ref jobStatus);
            }
        }
    }
}
