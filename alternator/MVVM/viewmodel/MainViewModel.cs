using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AsyncAwaitBestPractices.MVVM;
using NLog;

namespace guildwars2.tools.alternator
{
    class MainViewModel : ObservableObject
    {
        public IAsyncCommand<object>? LoginAllCommand { get; set; }
        private const string AccountsJson = "accounts.json";

        public MainViewModel()
        {
            SetLogging();

            LoginAllCommand = new AsyncCommand<object>(async o => {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                List<Account>? accounts;
                await using (var stream = File.OpenRead(AccountsJson))
                {
                    accounts = await JsonSerializer.DeserializeAsync<List<Account>>(stream);
                }

                var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")));
                var accountsToRun = accounts.Where(a => a.LastSuccess < DateTime.UtcNow.Date);
                await launcher.Launch(accountsToRun);

                File.WriteAllText(AccountsJson, JsonSerializer.Serialize(accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true }));

                await using (var stream = File.OpenWrite(AccountsJson))
                {
                    await JsonSerializer.SerializeAsync(stream, accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
                }

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
