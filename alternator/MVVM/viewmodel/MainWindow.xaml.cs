using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using NLog;

namespace guildwars2.tools.alternator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            SetLogging();

            InitializeComponent();

            //var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            //var accountsJson = "accounts.json";
            //var accounts = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(accountsJson));

            //var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")));
            //launcher.Launch(accounts.Where(a=> a.LastSuccess < DateTime.UtcNow.Date));

            //File.WriteAllText(accountsJson, JsonSerializer.Serialize(accounts, new JsonSerializerOptions{AllowTrailingCommas = true, WriteIndented = true}));
            //LogManager.Shutdown();
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
