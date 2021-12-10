using System;
using System.IO;
using AsyncAwaitBestPractices.MVVM;
using guildwars2.tools.alternator.core;
using NLog;

namespace guildwars2.tools.alternator
{
    class MainViewModel : ObservableObject
    {
        public IAsyncCommand<object>? LoginAllMultiCommand { get; set; }
        public IAsyncCommand<object>? LoginAllSingleCommand { get; set; }
        private const string AccountsJson = "accounts.json";

        public MainViewModel()
        {
            SetLogging();

            LoginAllMultiCommand = new AsyncCommand<object>(async o => {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")));
                var accountManager = new AccountManager(AccountsJson, launcher);

                await accountManager.Launch(4);

                LogManager.Shutdown();
            });

            LoginAllSingleCommand = new AsyncCommand<object>(async o => {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")));
                var accountManager = new AccountManager(AccountsJson, launcher);

                await accountManager.Launch(1);

                LogManager.Shutdown();
            });
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
