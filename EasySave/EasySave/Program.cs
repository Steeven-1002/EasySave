using EasySave.Services;
using EasySave.Core;
using LoggingLibrary;

namespace EasySave
{
    public static class Program
    {
        private static ConfigManager _configManager = null!;
        private static BackupManager _backupManager = null!;
        private static LocalizationService _localizationService = null!;
        private static StateManager _stateManager = null!;

        /// <summary>
        /// Entry point of the application.
        /// Initializes components and either parses command-line arguments or displays the menu.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public static void Main(string[] args)
        {
            InitializeComponents();

            if (args.Length > 0)
            {
                ParseCommandLine(args.ToString() ?? string.Empty);
            }
            else
            {
                DisplayMenu();
            }
            Console.WriteLine(_localizationService.GetString("GoodbyeMessage"));
        }

        /// <summary>
        /// Initializes the main parts of the application, such as configuration, localization, state, and backup managers.
        /// </summary>
        private static void InitializeComponents()
        {
            _configManager = ConfigManager.Instance;

            _localizationService = new LocalizationService(_configManager.Language);
            _stateManager = new StateManager(_configManager.StateFilePath);
            _backupManager = new BackupManager(_configManager);

            Console.WriteLine(_localizationService.GetString("WelcomeMessage"));
        }

        /// <summary>
        /// Parses command-line arguments to execute specific backup jobs.
        /// </summary>
        /// <param name="args">Command-line arguments specifying job indexes or ranges.</param>
        private static void ParseCommandLine(string args)
        {
            Console.WriteLine(_localizationService.GetString("CommandLineExecution"));
            List<int> jobIndexesToRun = new List<int>();
            if (args.Length > 0)
            {
                string[] ranges = args.Split(';', StringSplitOptions.RemoveEmptyEntries);
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

        /// <summary>
        /// Displays the main menu and processes user input until the user chooses to exit.
        /// </summary>
        private static void DisplayMenu()
        {
            bool exit = false;
            while (!exit)
            {
                Console.WriteLine($"\n{_localizationService.GetString("MainMenu_Title")}");
                Console.WriteLine($"1. {_localizationService.GetString("MainMenu_CreateJob")}");
                Console.WriteLine($"2. {_localizationService.GetString("MainMenu_ListJobs")}");
                Console.WriteLine($"3. {_localizationService.GetString("MainMenu_ExecuteJob")}");
                Console.WriteLine($"4. {_localizationService.GetString("MainMenu_ExecuteMultipleJobs")}");
                Console.WriteLine($"5. {_localizationService.GetString("MainMenu_DeleteJob")}");
                Console.WriteLine($"6. {_localizationService.GetString("MainMenu_ChangeLanguage")}");
                Console.WriteLine($"7. {_localizationService.GetString("MainMenu_ChangeLogFormat")}");
                Console.WriteLine($"8. {_localizationService.GetString("MainMenu_Exit")}");
                Console.Write($"{_localizationService.GetString("EnterChoice")}: ");

                string? choice = Console.ReadLine();
                if (choice == "8") exit = true;
                else ProcessUserChoice(choice);
            }
        }

        /// <summary>
        /// Processes the user's menu choice and calls the appropriate method.
        /// </summary>
        /// <param name="choice">The user's menu choice.</param>
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
                case "7": UiChangeLog(); break;
                case "8": break;
                default: Console.WriteLine(_localizationService.GetString("InvalidChoice")); break;
            }
            if (choice != "8" && choice != "6")
            {
                Console.WriteLine(_localizationService.GetString("PressEnterToContinue"));
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Allows the user to create a new backup job by providing the necessary details.
        /// </summary>
        private static void UiCreateBackupJob()
        {
            Console.WriteLine($"\n{_localizationService.GetString("CreateJob_Title")}");
            Console.Write($"{_localizationService.GetString("EnterJobName")}: ");
            string? name = Console.ReadLine();
            Console.Write($"{_localizationService.GetString("EnterSourcePath")}: ");
            string? source = Console.ReadLine();
            Console.Write($"{_localizationService.GetString("EnterTargetPath")}: ");
            string? target = Console.ReadLine();
            Console.Write($"{_localizationService.GetString("EnterBackupType")}: ");
            string? typeStr = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(typeStr))
            {
                Console.WriteLine(_localizationService.GetString("ErrorAllFieldsRequired"));
                return;
            }
            if (!Enum.TryParse(typeStr.ToUpperInvariant(), out BackupType type))
            {
                Console.WriteLine(_localizationService.GetString("ErrorInvalidBackupType"));
                return;
            }
            _backupManager.AddJob(name, source, target, type);
        }

        /// <summary>
        /// Lists all existing backup jobs with their details.
        /// </summary>
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
                var jobState = jobs[i].State; // Correctly reference the 'State' property of the job
                string statusInfo = $"{jobState}";
                Console.WriteLine($"{i + 1}. {jobs[i].Name} ({jobs[i].Type}) - Src: {jobs[i].SourcePath} -> Dest: {jobs[i].TargetPath} [State: {statusInfo}]");
            }
        }

        /// <summary>
        /// Executes a single backup job selected by the user.
        /// </summary>
        private static void UiExecuteSingleJob()
        {
            UiListBackupJobs();
            Console.Write($"{_localizationService.GetString("EnterJobIndexToExecute")}: ");
            if (int.TryParse(Console.ReadLine(), out int index) && index > 0 && index <= _backupManager.GetAllJobs().Count)
            {
                _backupManager.ExecuteJob(index - 1); // index 0-based
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("InvalidJobIndex"));
            }
        }

        /// <summary>
        /// Executes multiple backup jobs based on user input (e.g., ranges or specific indexes).
        /// </summary>
        private static void UiExecuteMultipleJobs()
        {
            UiListBackupJobs();
            Console.Write($"{_localizationService.GetString("EnterJobIndexesToExecute")}: "); // Ex: 1;3 ou 1-3
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine(_localizationService.GetString("NoIndexesProvided"));
            }
            else
            {
                ParseCommandLine(input);
            }
        }

        /// <summary>
        /// Deletes a backup job selected by the user.
        /// </summary>
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

        private static void UiChangeLog()
        {
            Console.WriteLine(_localizationService.GetString("ChangeLogFormat_Title"));
            Console.WriteLine(_localizationService.GetString("ChangeLogFormat_Xml"));
            Console.WriteLine(_localizationService.GetString("ChangeLogFormat_JSON"));
            Console.Write(_localizationService.GetString("ChangeLogFormat_Choice"));
            string? input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    LoggingBackup.RecreateInstance("XML");
                    Console.WriteLine(_localizationService.GetString("ChangeLogFormat_XmlValid"));
                    break;
                case "2":
                    LoggingBackup.RecreateInstance("JSON");
                    Console.WriteLine(_localizationService.GetString("ChangeLogFormat_JsonValid"));
                    break;
                default:
                    Console.WriteLine(_localizationService.GetString("ChangeLogFormat_Invalid"));
                    break;
            }
        }



        /// <summary>
        /// Allows the user to change the application's language.
        /// </summary>
        private static void UiChangeLanguage()
        {
            Console.Write($"{_localizationService.GetString("EnterLanguageCode")}: ");
            string? langCode = Console.ReadLine()?.ToLower();
            if (!string.IsNullOrWhiteSpace(langCode) && (langCode == "en" || langCode == "fr"))
            {
                if (_localizationService.LoadLanguage(langCode))
                {
                    _configManager.SetSetting("Language", langCode);
                    _configManager.SaveConfiguration();
                    Console.WriteLine(_localizationService.GetString("LanguageChangedSuccess", langCode));
                }
            }
            else
            {
                Console.WriteLine(_localizationService.GetString("InvalidLanguageCode"));
            }
        }
    }
}