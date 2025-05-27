using System;

namespace LoggingLibrary
{
    /// <summary>
    /// Fournit des services de journalisation pour enregistrer des entrées de log dans un fichier.
    /// </summary>
    public class LogService
    {
        private readonly LogFile _logFile;
        private readonly ILogFormatter _logFormatter;
        private readonly string _logDirectoryPath;
        private readonly object _logWriteLock = new();

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref="LogService"/>.
        /// </summary>
        /// <param name="logDirectoryPath">Le chemin du dossier où les fichiers de log seront stockés.</param>
        /// <param name="logFormatter">Le formateur utilisé pour formater les entrées de log.</param>
        /// <exception cref="ArgumentNullException">Déclenchée si <paramref name="logFormatter"/> est null.</exception>
        public LogService(string logDirectoryPath, ILogFormatter logFormatter)
        {
            _logDirectoryPath = logDirectoryPath;
            _logFile = new LogFile(logDirectoryPath, logFormatter);
            _logFormatter = logFormatter ?? throw new ArgumentNullException(nameof(logFormatter));
        }

        /// <summary>
        /// Journalise une entrée avec les détails spécifiés.
        /// </summary>
        /// <param name="timestamp">L'horodatage de l'entrée de log.</param>
        /// <param name="saveName">Le nom de l'opération de sauvegarde.</param>
        /// <param name="sourcePath">Le chemin source du fichier au format UNC.</param>
        /// <param name="targetPath">Le chemin cible du fichier au format UNC.</param>
        /// <param name="fileSize">La taille du fichier journalisé, en octets. Optionnel.</param>
        /// <param name="durationMs">La durée du transfert de fichier, en millisecondes. Optionnel.</param>
        /// <param name="encryptionTimeMs">La durée du chiffrement, en millisecondes. Optionnel.</param>
        /// <param name="details">Détails supplémentaires sur l'opération. Optionnel.</param>
        public void Log(
            DateTime timestamp,
            string saveName,
            string sourcePath,
            string targetPath,
            long? fileSize = null,
            double? durationMs = null,
            double? encryptionTimeMs = null,
            string? details = null)
        {
            var logEntry = new LogEntry
            {
                Timestamp = timestamp,
                SaveName = saveName,
                SourcePathUNC = sourcePath,
                TargetPathUNC = targetPath,
                FileSize = fileSize,
                FileTransferTimeMs = durationMs,
                EncryptionTimsMs = encryptionTimeMs,
                Details = details
            };

            string formattedLog = _logFormatter.FormatLog(logEntry);

            lock (_logWriteLock)
            {
                _logFile.WriteLogEntry(formattedLog);
            }
        }

        /// <summary>
        /// Retourne le chemin du dossier où les fichiers de log sont stockés.
        /// </summary>
        /// <returns>Le chemin du dossier sous forme de chaîne.</returns>
        public string GetlogDirectoryPath()
        {
            return _logDirectoryPath;
        }
    }
}
