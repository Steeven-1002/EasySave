# Documentation de la Bibliothèque `LoggingLibrary`

Cette bibliothèque permet une journalisation des détails d'opérations de fichiers. Elle prend en charge différentes options de formatage de journaux (JSON et XML).

## Interfaces

### `ILogFormatter.cs`
* **Objectif**: Définit les méthodes pour formater les entrées de journal et fusionner le nouveau contenu de journal dans le contenu existant.
* **Méthodes**:
    * `FormatLog(LogEntry logEntry)`: Formate un objet `LogEntry` en une représentation textuelle.
    * `MergeLogContent(string existingContent, string newContent)`: Fusionne le nouveau contenu d'entrée de journal avec le contenu de journal existant.

### `ILoggingService.cs`
* **Objectif**: Définit un service de journalisation pour enregistrer les opérations de fichiers et récupérer les informations du fichier journal.
* **Méthodes**:
    * `Log(DateTime timestamp, string saveName, string sourcePath, string targetPath, long? fileSize = null, double? durationMs = null, string? details = null)`: Enregistre les détails d'une opération de fichier.
    * `GetLogFilePath()`: Obtient le chemin du fichier journal.
    * `CloseLogFile()`: Ferme le fichier journal et libère les ressources associées.

## Classes

### `JsonLogFormatter.cs`
* **Objectif**: Une implémentation de `ILogFormatter` qui sérialise les entrées de journal au format JSON.
* **Constructeur**:
    * `JsonLogFormatter()`: Initialise une nouvelle instance avec les options de sérialisation JSON par défaut (écriture indentée, valeurs nulles ignorées).
* **Méthodes**:
    * `FormatLog(LogEntry logEntry)`: Sérialise une `LogEntry` en une chaîne JSON.
    * `MergeLogContent(string existingContent, string newContent)`: Ajoute une nouvelle entrée de journal au contenu JSON existant, gérant la création initiale du tableau et l'ajout ultérieur.

### `LogEntry.cs`
* **Objectif**: Représente une seule entrée de journal contenant les détails d'une opération de fichier.
* **Propriétés**:
    * `SaveName` (string): Le nom sous lequel le fichier est enregistré.
    * `SourcePathUNC` (string): Le chemin UNC du fichier source.
    * `TargetPathUNC` (string): Le chemin UNC du fichier cible.
    * `FileSize` (long?, facultatif): La taille du fichier en octets.
    * `FileTransferTimeMs` (double?, facultatif): La durée du transfert de fichier en millisecondes.
    * `Timestamp` (DateTime): L'horodatage de l'opération.
    * `EncryptionTimsMs` (double?, facultatif): La durée du processus de chiffrement en millisecondes.
    * `Details` (string?, facultatif): Détails supplémentaires sur l'opération.
* **Classe `DateTimeFormatConverter`**: Un convertisseur JSON personnalisé pour les objets `DateTime`, assurant la sérialisation et la désérialisation au format "dd/MM/yyyy HH:mm:ss".

### `LogFile.cs`
* **Objectif**: Gère l'écriture des entrées de journal dans les fichiers, y compris la rotation des fichiers en fonction de la taille et la nomination quotidienne.
* **Constructeur**:
    * `LogFile(string logDirectoryPath, ILogFormatter logFormatter)`: Initialise une nouvelle instance, créant le répertoire de journalisation s'il n'existe pas.
* **Méthodes**:
    * `WriteLogEntry(string logEntry)`: Ajoute une entrée de journal formatée à un tampon interne et la vide dans le fichier.
    * `FlushBuffer()`: Écrit le contenu du journal tamponné dans le fichier journal actuel, gérant les conflits d'accès aux fichiers avec une logique de réessai.
* **Logique Interne**:
    * Les fichiers journaux sont nommés en fonction de la date (par exemple, "AAAA-MM-JJ_X.json" ou "AAAA-MM-JJ_X.xml").
    * De nouveaux fichiers journaux sont créés si le fichier actuel dépasse `MaxLogFileSizeBytes` (100 Ko).

### `LogService.cs`
* **Objectif**: Fournit la fonctionnalité de journalisation principale, utilisant `LogFile` et `ILogFormatter` pour enregistrer les entrées de journal.
* **Constructeur**:
    * `LogService(string logDirectoryPath, ILogFormatter logFormatter)`: Initialise le service avec un répertoire de journalisation et un formateur spécifiés.
* **Méthodes**:
    * `Log(DateTime timestamp, string saveName, string sourcePath, string targetPath, long? fileSize = null, double? durationMs = null, double? encryptionTimeMs = null, string? details = null)`: Crée une `LogEntry` à partir des paramètres fournis, la formate à l'aide du formateur injecté et l'écrit dans le fichier journal.
    * `GetlogDirectoryPath()`: Retourne le chemin du répertoire où les fichiers journaux sont stockés.

### `XmlLogFormatter.cs`
* **Objectif**: Une implémentation de `ILogFormatter` qui sérialise les entrées de journal au format XML.
* **Méthodes**:
    * `FormatLog(LogEntry logEntry)`: Sérialise une `LogEntry` en une chaîne XML, omettant la déclaration XML et indentant la sortie.
    * `MergeLogContent(string existingContent, string newContent)`: Fusionne une nouvelle entrée de journal dans le contenu XML existant, garantissant que la sortie reste une structure XML valide avec un élément racine `<LogEntries>`.
