using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NLog;

namespace guildwars2.tools.alternator
{
    class MainViewModel : ObservableObject
    {
        public RelayCommand? LoginAllCommand { get; set; }

        public MainViewModel()
        {
            LoginAllCommand = new RelayCommand(o => {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                var accountsJson = "accounts.json";
                var accounts = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(accountsJson));

                var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")));
                launcher.Launch(accounts.Where(a => a.LastSuccess < DateTime.UtcNow.Date));

                File.WriteAllText(accountsJson, JsonSerializer.Serialize(accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true }));
                LogManager.Shutdown();
            });
        }
    }
}
