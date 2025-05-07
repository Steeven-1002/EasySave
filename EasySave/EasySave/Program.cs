using System;
using System.IO;
using System.Collections.Generic;
using LoggingLibrary;
using System.Text.Json;
using System.Linq;

namespace EasySave
{
    public class Program : IStateObserver
    {
        private ConfigManager _configManager;
        private BackupManager _backupManager;
        private LocalizationService _localizationService;
        private FileSystemService _fileSystemService;
        private LogService _logService;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.InitializeComponents(args);
            program.Run();
        }

        public void InitializeComponents(string[] args)
        {
            // Initialise les composants de l'application
            _configManager = new ConfigManager("config.json", "log.json", "state.json");
            _configManager.LoadConfiguration();

            // Charge la langue par défaut ou la langue enregistrée
            string initialLanguage = _configManager.GetSetting("currentLanguage") as string ?? _configManager.GetSetting("defaultLanguage") as string ?? "en";
            _localizationService = new LocalizationService(_configManager.GetSetting("defaultLanguage") as string ?? "en");
            _localizationService.LoadLanguage(initialLanguage);

            _fileSystemService = new FileSystemService();
            _logService = InitializeLogService();

            _backupManager = new BackupManager(_fileSystemService, _logService);
            _backupManager.LoadJobs();
            _backupManager.StateManager.RegisterObserver(this); // S'enregistre pour les notifications d'état
        }

        private LogService InitializeLogService()
        {
            // Initialise le service de journalisation (à adapter selon votre implémentation de LoggingLibrary)
            string logDirectory = _configManager.GetSetting("logDirectory") as string ?? @"C:\Logs\EasySave";
            string logFileName = $"EasySave_{DateTime.Now:yyyy-MM-dd}.json";
            string logFilePath = Path.Combine(logDirectory, logFileName);
            ILogFormatter jsonFormatter = new JsonLogFormatter();
            LogService logService = new LogService(logFilePath, jsonFormatter);
            return logService;
        }

        public void Run()
        {
            // Flux principal de l'application
            SelectLanguage();
            DisplayHomeScreen();
        }

        private void SelectLanguage()
        {
            // Permet à l'utilisateur de choisir la langue
            Console.WriteLine("Select Language (en/fr): ");
            string languageChoice = Console.ReadLine().ToLower();
            if (languageChoice == "fr")
            {
                _localizationService.LoadLanguage("fr");
                _configManager.SetSetting("currentLanguage", "fr");
            }
            else
            {
                _localizationService.LoadLanguage("en");
                _configManager.SetSetting("currentLanguage", "en");
            }
        }

        private void DisplayHomeScreen()
        {
            // Affiche l'écran d'accueil de l'application
            bool continueRunning = true;
            while (continueRunning)
            {
                Console.WriteLine("\n=== EasySave - " + _localizationService.GetString("homeScreen") + " ==="); // Écran d'accueil
                Console.WriteLine("1. " + _localizationService.GetString("createLoadBackup")); // Créer/charger une sauvegarde ou quitter
                Console.WriteLine("2. " + _localizationService.GetString("exit")); // Quitter

                Console.Write(_localizationService.GetString("enterChoice") + " "); // Entrez votre choix :
                if (int.TryParse(Console.ReadLine(), out int choice))
                {
                    continueRunning = ProcessHomeScreenChoice(choice);
                }
                else
                {
                    Console.WriteLine(_localizationService.GetString("invalidChoice")); // Choix invalide.
                }
            }
        }

        private bool ProcessHomeScreenChoice(int choice)
        {
            // Traite le choix de l'utilisateur sur l'écran d'accueil
            switch (choice)
            {
                case 1:
                    DisplayBackupMenu();
                    return true;
                case 2:
                    SaveAndExit();
                    return false;
                default:
                    Console.WriteLine(_localizationService.GetString("invalidChoice")); // Choix invalide.
                    return true;
            }
        }

        private void DisplayBackupMenu()
        {
            // Affiche le menu de sauvegarde
            bool continueRunning = true;
            while (continueRunning)
            {
                Console.WriteLine("\n=== EasySave - " + _localizationService.GetString("backupMenu") + " ==="); // Menu de sauvegarde
                Console.WriteLine("1. " + _localizationService.GetString("createBackup")); // Créer une sauvegarde
                Console.WriteLine("2. " + _localizationService.GetString("loadBackup")); // Charger une sauvegarde
                Console.WriteLine("3. " + _localizationService.GetString("backToHome")); // Retour à l'écran d'accueil

                Console.Write(_localizationService.GetString("enterChoice") + " "); // Entrez votre choix :
                if (int.TryParse(Console.ReadLine(), out int choice))
                {
                    continueRunning = ProcessBackupMenuChoice(choice);
                }
                else
                {
                    Console.WriteLine(_localizationService.GetString("invalidChoice")); // Choix invalide.
                }
            }
        }

        private bool ProcessBackupMenuChoice(int choice)
        {
            // Traite le choix de l'utilisateur dans le menu de sauvegarde
            switch (choice)
            {
                case 1:
                    CreateBackupJob();
                    return true;
                case 2:
                    LoadBackupJobs();
                    return true;
                case 3:
                    DisplayHomeScreen();
                    return false;
                default:
                    Console.WriteLine(_localizationService.GetString("invalidChoice")); // Choix invalide.
                    return true;
            }
        }

        private void CreateBackupJob()
        {
            // Crée une nouvelle tâche de sauvegarde
            if (_backupManager.backupJobs.Count >= 5)
            {
                Console.WriteLine(_localizationService.GetString("maxBackupsReached")); // Limite de 5 sauvegardes atteinte.
                DeleteBackupJob();
            }

            Console.WriteLine("\n=== " + _localizationService.GetString("createBackup") + " ==="); // Créer une sauvegarde

            Console.Write(_localizationService.GetString("enterJobName") + " "); // Entrez le nom de la sauvegarde :
            string name = Console.ReadLine();

            Console.Write(_localizationService.GetString("enterSourcePath") + " "); // Entrez le répertoire source :
            string sourcePath = Console.ReadLine();

            Console.Write(_localizationService.GetString("enterTargetPath") + " "); // Entrez le répertoire de destination :
            string targetPath = Console.ReadLine();

            Console.WriteLine("1. " + _localizationService.GetString("fullBackup")); // Sauvegarde complète
            Console.WriteLine("2. " + _localizationService.GetString("differentialBackup")); // Sauvegarde différentielle
            Console.Write(_localizationService.GetString("enterBackupType") + " "); // Choisissez le type de sauvegarde (1 ou 2) :
            if (int.TryParse(Console.ReadLine(), out int backupTypeChoice))
            {
                BackupType type = (backupTypeChoice == 1) ? BackupType.Full : (backupTypeChoice == 2) ? BackupType.Differential : BackupType.Full; // Default to Full

                BackupStrategy strategy = null;
                if (type == BackupType.Full)
                {
                    strategy = new FullBackupStrategy(_fileSystemService, _logService);
                }
                else if (type == BackupType.Differential)
                {
                    strategy = new DifferentialBackupStrategy(_fileSystemService, _logService);
                    Console.Write(_localizationService.GetString("enterFullBackupPart") + " "); // Entrez la partie de la sauvegarde complète :
                    string fullBackupPart = Console.ReadLine(); // Vous devrez peut-être gérer cette partie dans votre DifferentialBackupStrategy
                }

                if (strategy != null)
                {
                    _backupManager.AddJob(name, sourcePath, targetPath, type, new BackupJob() { BackupStrategy = strategy });
                    Console.WriteLine(_localizationService.GetString("jobCreated")); // Tâche de sauvegarde créée.
                }
                else
                {
                    Console.WriteLine(_localizationService.GetString("invalidBackupType")); // Type de sauvegarde invalide.
                }

                // Crée les entrées dans les fichiers de log et d'état
                _logService.Log(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    EventType = "JobCreated",
                    Message = $"{_localizationService.GetString("job")} '{name}' {_localizationService.GetString("created")}" // La tâche, a été créée
                });
                _backupManager.StateManager.SaveState();
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("invalidInput")); // Entrée invalide.
            }
        }

        private void LoadBackupJobs()
        {
            // Permet à l'utilisateur de charger et d'exécuter des tâches de sauvegarde existantes
            Console.WriteLine("\n=== " + _localizationService.GetString("loadBackup") + " ==="); // Charger une sauvegarde
            ListBackupJobs();

            Console.WriteLine(_localizationService.GetString("selectJobsToLoad")); // Sélectionnez les sauvegardes à charger (séparées par des virgules) :
            string input = Console.ReadLine();
            List<int> jobIndices = input.Split(',').Select(s => int.TryParse(s.Trim(), out int n) ? n : -1).Where(n => n >= 0 && n < _backupManager.backupJobs.Count).ToList();

            if (jobIndices.Any())
            {
                foreach (int index in jobIndices)
                {
                    _backupManager.ExecuteJob(index);

                    // Les mises à jour du journal et de l'état sont gérées dans ExecuteJob
                }
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("noValidJobsSelected")); // Aucune sauvegarde valide sélectionnée.
            }
        }

        private void ListBackupJobs()
        {
            // Affiche la liste des tâches de sauvegarde existantes
            List<BackupJob> jobs = _backupManager.backupJobs;
            if (jobs.Count == 0)
            {
                Console.WriteLine(_localizationService.GetString("noJobs")); // Aucune tâche de sauvegarde enregistrée.
            }
            else
            {
                Console.WriteLine("\n=== " + _localizationService.GetString("jobListHeader") + " ==="); // Liste des Tâches de Sauvegarde
                for (int i = 0; i < jobs.Count; i++)
                {
                    Console.WriteLine($"{i}: {jobs[i].Name} ({jobs[i].BackupType}) - {jobs[i].SourcePath} -> {jobs[i].TargetPath} - {_localizationService.GetString("state")}: {jobs[i].BackupState}"); // État:
                }
            }
        }

        private void DeleteBackupJob()
        {
            // Permet à l'utilisateur de supprimer une tâche de sauvegarde
            Console.WriteLine("\n=== " + _localizationService.GetString("deleteBackup") + " ==="); // Supprimer une sauvegarde
            ListBackupJobs();

            Console.Write(_localizationService.GetString("enterJobIndexToRemove") + " "); // Entrez l'index de la sauvegarde à supprimer :
            if (int.TryParse(Console.ReadLine(), out int jobIndex))
            {
                if (_backupManager.RemoveJob(jobIndex))
                {
                    Console.WriteLine(_localizationService.GetString("jobRemoved")); // Tâche supprimée.
                }
                else
                {
                    Console.WriteLine(_localizationService.GetString("invalidIndex")); // Index invalide.
                }
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("invalidIndex")); // Index invalide.
            }
        }

        private void SaveAndExit()
        {
            // Sauvegarde les configurations et les tâches avant de quitter
            _configManager.SaveConfiguration();
            _backupManager.SaveJobs();
            Console.WriteLine(_localizationService.GetString("savingAndExiting")); // Sauvegarde et fermeture de l'application...
        }

        public void OnStateChange(string jobName, BackupState newState)
        {
            // Méthode appelée par StateManager pour notifier les changements d'état
            _logService.Log(new LogEntry
            {
                Timestamp = DateTime.Now,
                EventType = "JobStateChanged",
                Message = $"{_localizationService.GetString("job")} '{jobName}' {_localizationService.GetString("stateChangedTo")} {newState}" // La tâche, a changé son état pour
            });
            Console.WriteLine($"{_localizationService.GetString("job")} '{jobName}' {_localizationService.GetString("stateChangedToConsole")} {newState}"); // La tâche, a changé son état pour :
        }
    }
}