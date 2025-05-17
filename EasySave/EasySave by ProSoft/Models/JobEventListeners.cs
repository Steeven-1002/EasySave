using System;

namespace EasySave_by_ProSoft.Models {
    public interface JobEventListeners {
        void Update(ref JobStatus jobStatus);
    }
}
