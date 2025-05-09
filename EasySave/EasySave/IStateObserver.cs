using EasySave.Models;

namespace EasySave.Interfaces
{
    /// <summary>
    /// Interface permettant d'observer les changements d'état d'un processus de sauvegarde.
    /// </summary>
    public interface IStateObserver
    {
        /// <summary>
        /// Méthode appelée lorsque l'état d'un travail de sauvegarde change.
        /// </summary>
        /// <param name="jobName">Nom du travail de sauvegarde.</param>
        /// <param name="newState">Nouvel état du travail de sauvegarde.</param>
        /// <param name="totalFiles">Nombre total de fichiers à sauvegarder.</param>
        /// <param name="totalSize">Taille totale des fichiers à sauvegarder, en octets.</param>
        /// <param name="remainingFiles">Nombre de fichiers restants à sauvegarder.</param>
        /// <param name="remainingSize">Taille restante des fichiers à sauvegarder, en octets.</param>
        /// <param name="currentSourceFile">Chemin du fichier source actuellement traité.</param>
        /// <param name="currentTargetFile">Chemin du fichier cible actuellement traité.</param>
        void StateChanged(
            string jobName,
            BackupState newState,
            int totalFiles,
            long totalSize,
            int remainingFiles,
            long remainingSize,
            string currentSourceFile,
            string currentTargetFile);
    }
}