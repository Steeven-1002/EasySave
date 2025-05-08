namespace EasySave.Models
{
    // Utilisé par IBackupObserver.
    // Si c'est une typo dans le diagramme et que IBackupObserver.Update devrait prendre BackupState,
    // cette énumération pourrait ne pas être nécessaire.
    public enum BackupStatus
    {
        STARTED,
        IN_PROGRESS,
        FILE_COPIED,
        COMPLETED_SUCCESS,
        COMPLETED_WITH_ERRORS,
        ERROR
    }
}