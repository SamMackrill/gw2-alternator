using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices.MVVM;
using guildwars2.tools.alternator.core;
using NLog;

namespace guildwars2.tools.alternator
{
    public class MainViewModel : ObservableObject
    {
        public IAsyncCommand? LoginAllMultiCommand { get; set; }
        public IAsyncCommand? LoginAllSingleCommand { get; set; }
        public IAsyncCommand? CollectCommand { get; set; }
        public IAsyncCommand? UpdateCommand { get; set; }
        public IAsyncCommand? StopCommand { get; set; }
        public IAsyncCommand? ShowSettingsCommand { get; set; }

        public string TimeUtc { get; set; }

        private const string AccountsJson = "accounts.json";
        private CancellationTokenSource cts;
        private readonly string appData;


        private bool running;
        private bool Running {
            get => running;
            set
            {
                running = value;
                LoginAllMultiCommand?.RaiseCanExecuteChanged();
                LoginAllSingleCommand?.RaiseCanExecuteChanged();
                CollectCommand?.RaiseCanExecuteChanged();
                UpdateCommand?.RaiseCanExecuteChanged();
                StopCommand?.RaiseCanExecuteChanged();
                ShowSettingsCommand?.RaiseCanExecuteChanged();
            }
        }

        public MainViewModel()
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            SetLogging();

            async Task Login(int maxInstances, LaunchType launchType)
            {
                try
                {
                    Running = true;

                    cts = new CancellationTokenSource();
                    cts.Token.ThrowIfCancellationRequested();
                    var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")), launchType);
                    var accountManager = new AccountManager(AccountsJson, launcher);

                    await accountManager.Launch(maxInstances, launchType, cts.Token);

                    LogManager.Shutdown();
                }
                finally
                {
                    Running = false;
                }
            }

            LoginAllMultiCommand = new AsyncCommand(async () => { await Login(4, LaunchType.LaunchNeeded); }, o => !Running);
            LoginAllSingleCommand = new AsyncCommand(async () => { await Login(1, LaunchType.LaunchNeeded); }, o => !Running);
            CollectCommand = new AsyncCommand(async () => { await Login(2, LaunchType.CollectNeeded); }, o => !Running);
            UpdateCommand = new AsyncCommand(async () => { await Login(1, LaunchType.UpdateAll); }, o => !Running);

            StopCommand = new AsyncCommand(async () => cts?.Cancel(), o => Running);
            ShowSettingsCommand = new AsyncCommand(async () =>
            {
                
            }, o => !Running);
        }

        private void SetLogging()
        {
            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "alternator-log.txt" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Apply config           
            LogManager.Configuration = config;
        }
    }
}
