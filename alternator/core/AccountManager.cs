using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace guildwars2.tools.alternator.core
{
    public class AccountManager
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public List<Account>? Accounts { get; set; }
        private readonly string accountsJson;
        private readonly ClientController clientController;

        public AccountManager(string accountsJson, ClientController clientController)
        {
            this.accountsJson = accountsJson;
            this.clientController = clientController;
        }

        public async Task Save()
        {
            await using var stream = File.OpenWrite(accountsJson);
            await JsonSerializer.SerializeAsync(stream, Accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            Logger.Debug("Accounts saved to {0}", accountsJson);
        }

        public async Task Load()
        {
            await using var stream = File.OpenRead(accountsJson);
            Accounts = await JsonSerializer.DeserializeAsync<List<Account>>(stream);
            Logger.Debug("Accounts loaded from {0}", accountsJson);
        }

        public async Task Launch(int maxInstances)
        {
            await Load();
            if (Accounts == null) return;
            var accountsToRun = Accounts.Where(a => a.LastSuccess < DateTime.UtcNow.Date);
            await clientController.Launch(this, maxInstances);
            await Save();
        }
    }
}
