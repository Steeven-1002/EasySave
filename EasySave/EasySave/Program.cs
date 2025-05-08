using System;
using System.Collections.Generic;
using System.Linq;
using EasySave.Models;
using EasySave.Interfaces;
using EasySave.Services;
using EasySave.Core;
using System.Text.Json;

// Placeholder pour la DLL externe
namespace LoggingDLLNamespace
{
    public class LoggingDLL : EasySave.Interfaces.LoggingService
    {
        private string _logFilePathBase; // ex: "Logs" directory
        private string GetTodaysLogFilePath() => Path.Combine(_logFilePathBase, $"log_{DateTime.Now:yyyy-MM-dd}.json");

        public LoggingDLL(string logDirectory = "Logs")
        {
            _logFilePathBase = logDirectory;
            if (!Directory.Exists(_logFilePathBase))
            {
                Directory.CreateDirectory(_logFilePathBase);
            }
            Console.WriteLine($"LoggingService (DLL Placeholder) initialized. Logs in: {_logFilePathBase}");
        }

        public string GetLogFilePath() => GetTodaysLogFilePath();

        public void WriteLog(DateTime timestamp, string jobName, string sourcePath, string targetPath, long fileSize, long transferTime)
        {
            string logMessage = $"{timestamp:G} - Job: {jobName}, Src: {sourcePath}, Dest: {targetPath}, Size: {fileSize}B, Time: {transferTime}ms";
            Console.WriteLine($"DLL_LOG: {logMessage}"); // Pour le débogage console
            try
            {
                var logEntry = new { timestamp, jobName, sourcePath, targetPath, fileSize, transferTime };
                string jsonEntry = JsonSerializer.Serialize(logEntry);
                File.AppendAllText(GetTodaysLogFilePath(), jsonEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoggingDLL ERROR writing log: {ex.Message}");
            }
        }

        public void Update(BackupJob job, BackupStatus status)
        {
            long fileSize = 0; // Non applicable directement pour un statut général
            long transferTime = 0; // Idem
            WriteLog(DateTime.Now, job.Name, $"Status update: {status}", "N/A", fileSize, transferTime);
        }

        public void StateChanged(JobState state)
        {
            WriteLog(state.Timestamp, state.JobName, state.CurrentSourceFile, state.CurrentTargetFile, 0, 0); // Simplifié
        }
    }
}


namespace EasySave.ConsoleApp
{
    public class Program
    {
        private static ConfigManager _configManager = null!; // Initialisé dans InitializeComponents
        private static BackupManager _backupManager = null!; // Initialisé dans InitializeComponents
        private static LocalizationService _localizationService = null!; // Initialisé dans InitializeComponents
        private static StateManager _stateManager = null!; // Initialisé dans InitializeComponents
        private static LoggingService _loggingService = null!; // Initialisé dans InitializeComponents

        public static void Main(string[] args)
        {
            InitializeComponents();

            if (args.Length > 0)
            {
                ParseCommandLine(args);
            }
            else
            {
                DisplayMenu();
            }
            Console.WriteLine(_localizationService.GetString("GoodbyeMessage"));
        }

        private static void InitializeComponents()
        {
            _configManager = new ConfigManager("app_settings.json"); // Ou autre nom
            _localizationService = new LocalizationService(_configManager.Language); // Utilise la langue de config

            // Le chemin du StateFile est géré par ConfigManager
            _stateManager = new StateManager(_configManager.StateFilePath);

            // Le chemin du LogFile (répertoire) est géré par ConfigManager
            // Ici on instancie notre placeholder. En réalité, ce serait une réf à la DLL.
            _loggingService = new LoggingDLLNamespace.LoggingDLL(_configManager.LogFilePath);

            // Enregistrement du logger comme observateur de l'état
            _stateManager.RegisterObserver(_loggingService);

            // BackupManager a besoin de StateManager
            _backupManager = new BackupManager(_stateManager, _configManager); // BackupManager utilise aussi ConfigManager pour le chemin des jobs

            // Les stratégies sont créées par BackupManager. Si elles doivent notifier
            // _loggingService, BackupManager doit le savoir pour enregistrer _loggingService
            // auprès des stratégies/jobs qu'il crée. C'est une complexité supplémentaire.
            // Pour l'instant, le diagramme montre LoggingService implémentant IBackupObserver,
            // donc il pourrait être passé aux stratégies ou aux jobs.

            Console.WriteLine(_localizationService.GetString("WelcomeMessage"));
        }

        private static void ParseCommandLine(string[] args)
        {
            Console.WriteLine(_localizationService.GetString("CommandLineExecution"));
            List<int> jobIndexesToRun = new List<int>();
            // TODO: Implémenter un parsing robuste pour "1-3" et "1;3"
            // Ce parsing est un placeholder et doit être amélioré
            if (args.Length > 0)
            {
                string[] ranges = args[0].Split(';');
                foreach (string range in ranges)
                {
                    if (range.Contains('-'))
                    {
                        string[] parts = range.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                        {
                            for (int i = start; i <= end; i++) jobIndexesToRun.Add(i - 1); // index 0-based
                        }
                    }
                    else if (int.TryParse(range, out int index))
                    {
                        jobIndexesToRun.Add(index - 1); // index 0-based
                    }
                }
            }

            if (jobIndexesToRun.Any())
            {
                var distinctIndexes = jobIndexesToRun.Distinct().ToArray();
                Console.WriteLine(_localizationService.GetString("ExecutingJobsViaCLI", string.Join(", ", distinctIndexes.Select(i => i + 1))));
                _backupManager.ExecuteJobs(distinctIndexes);
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("NoValidJobsForCLI"));
            }
        }

        private static void DisplayMenu()
        {
            bool exit = false;
            while (!exit)
            {
                Console.WriteLine($"\n{_localizationService.GetString("MainMenu_Title")}");
                Console.WriteLine($"1. {_localizationService.GetString("MainMenu_CreateJob")}");
                Console.WriteLine($"2. {_localizationService.GetString("MainMenu_ListJobs")}");
                Console.WriteLine($"3. {_localizationService.GetString("MainMenu_ExecuteJob")}"); // Exécuter un job spécifique
                Console.WriteLine($"4. {_localizationService.GetString("MainMenu_ExecuteMultipleJobs")}"); // Exécuter plusieurs jobs
                Console.WriteLine($"5. {_localizationService.GetString("MainMenu_DeleteJob")}");
                Console.WriteLine($"6. {_localizationService.GetString("MainMenu_ChangeLanguage")}");
                Console.WriteLine($"7. {_localizationService.GetString("MainMenu_Exit")}");
                Console.Write($"{_localizationService.GetString("EnterChoice")}: ");

                string? choice = Console.ReadLine();
                if (choice == "7") exit = true;
                else ProcessUserChoice(choice);
            }
        }

        private static void ProcessUserChoice(string? choice)
        {
            switch (choice)
            {
                case "1": UiCreateBackupJob(); break;
                case "2": UiListBackupJobs(); break;
                case "3": UiExecuteSingleJob(); break;
                case "4": UiExecuteMultipleJobs(); break;
                case "5": UiDeleteBackupJob(); break;
                case "6": UiChangeLanguage(); break;
                case "7": break; // Géré par la boucle DisplayMenu
                default: Console.WriteLine(_localizationService.GetString("InvalidChoice")); break;
            }
            if (choice != "7" && choice != "6")
            {
                Console.WriteLine(_localizationService.GetString("PressEnterToContinue"));
                Console.ReadLine();
            }
        }

        private static void UiCreateBackupJob()
        {
            Console.WriteLine($"\n{_localizationService.GetString("CreateJob_Title")}");
            Console.Write($"{_localizationService.GetString("EnterJobName")}: ");
            string? name = Console.ReadLine();
            Console.Write($"{_localizationService.GetString("EnterSourcePath")}: ");
            string? source = Console.ReadLine();
            Console.Write($"{_localizationService.GetString("EnterTargetPath")}: ");
            string? target = Console.ReadLine();
            Console.Write($"{_localizationService.GetString("EnterBackupType")} (FULL/DIFFERENTIAL): ");
            string? typeStr = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(typeStr))
            {
                Console.WriteLine(_localizationService.GetString("ErrorAllFieldsRequired"));
                return;
            }
            if (!Enum.TryParse<BackupType>(typeStr.ToUpperInvariant(), out BackupType type))
            {
                Console.WriteLine(_localizationService.GetString("ErrorInvalidBackupType"));
                return;
            }
            _backupManager.AddJob(name, source, target, type);
            // Les messages de succès/erreur de AddJob sont déjà dans BackupManager
        }

        private static void UiListBackupJobs()
        {
            var jobs = _backupManager.GetAllJobs();
            Console.WriteLine($"\n{_localizationService.GetString("BackupJobsList")}");
            if (!jobs.Any())
            {
                Console.WriteLine(_localizationService.GetString("NoJobsFound"));
                return;
            }
            for (int i = 0; i < jobs.Count; i++)
            {
                var jobState = _stateManager.GetState(jobs[i].Name);
                string statusInfo = jobState != null ? $"{jobState.State} ({jobState.RemainingFiles} files left)" : "N/A"; // Exemple
                Console.WriteLine($"{i + 1}. {jobs[i].Name} ({jobs[i].Type}) - Src: {jobs[i].SourcePath} -> Dest: {jobs[i].TargetPath} [State: {statusInfo}]");
            }
        }

        private static void UiExecuteSingleJob()
        {
            UiListBackupJobs();
            Console.Write($"{_localizationService.GetString("EnterJobIndexToExecute")}: ");
            if (int.TryParse(Console.ReadLine(), out int index) && index > 0 && index <= _backupManager.GetAllJobs().Count)
            {
                _backupManager.ExecuteJob(index - 1); // Index 0-based pour la liste
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("InvalidJobIndex"));
            }
        }

        private static void UiExecuteMultipleJobs()
        {
            UiListBackupJobs();
            Console.Write($"{_localizationService.GetString("EnterJobIndexesToExecute")}: "); // Ex: 1;3 ou 1-3
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine(_localizationService.GetString("NoIndexesProvided"));
                return;
            }

            List<int> jobIndexesToRun = new List<int>();
            // Logique de parsing similaire à ParseCommandLine
            string[] ranges = input.Split(';');
            foreach (string range in ranges)
            {
                if (range.Contains('-'))
                {
                    string[] parts = range.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                    {
                        for (int i = start; i <= end; i++) jobIndexesToRun.Add(i - 1);
                    }
                }
                else if (int.TryParse(range, out int index))
                {
                    jobIndexesToRun.Add(index - 1);
                }
            }
            var validIndexes = jobIndexesToRun.Distinct().Where(i => i >= 0 && i < _backupManager.GetAllJobs().Count).ToArray();
            if (validIndexes.Length > 0)
            {
                _backupManager.ExecuteJobs(validIndexes);
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("NoValidJobsSelected"));
            }
        }


        private static void UiDeleteBackupJob()
        {
            UiListBackupJobs();
            Console.Write($"{_localizationService.GetString("EnterJobIndexToDelete")}: ");
            if (int.TryParse(Console.ReadLine(), out int index) && index > 0 && index <= _backupManager.GetAllJobs().Count)
            {
                var jobToDelete = _backupManager.GetJob(index - 1);
                if (jobToDelete != null)
                {
                    Console.Write(_localizationService.GetString("ConfirmDelete", jobToDelete.Name));
                    if (Console.ReadLine()?.Trim().ToLower() == _localizationService.GetString("Yes").ToLower())
                    {
                        _backupManager.RemoveJob(index - 1);
                    }
                    else
                    {
                        Console.WriteLine(_localizationService.GetString("DeleteCancelled"));
                    }
                }
                else
                {
                    Console.WriteLine(_localizationService.GetString("InvalidJobIndex"));
                }
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("InvalidJobIndex"));
            }
        }

        private static void UiChangeLanguage()
        {
            Console.Write($"{_localizationService.GetString("EnterLanguageCode")} (en, fr): ");
            string? langCode = Console.ReadLine()?.ToLower();
            if (!string.IsNullOrWhiteSpace(langCode) && (langCode == "en" || langCode == "fr"))
            {
                if (_localizationService.LoadLanguage(langCode))
                {
                    _configManager.SetSetting("Language", langCode);
                    _configManager.SaveConfiguration();
                    Console.WriteLine(_localizationService.GetString("LanguageChangedSuccess", langCode));
                }
                // else: L'erreur est déjà affichée par LoadLanguage
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("InvalidLanguageCode"));
            }
        }
    }
}