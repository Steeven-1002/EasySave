namespace EasySave
{
    public interface IStateObserver
    {
        void OnStateChange(string jobName, BackupState newState);
    }
}