using EasySave.Models;

namespace EasySave.Interfaces
{
    public interface IStateObserver
    {
        void StateChanged(JobState state);
    }
}