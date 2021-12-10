using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace guildwars2.tools.alternator.core
{
    public class AccountManager
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public List<Account>? Accounts { get; set; }
        private readonly string accountsJson;
        private readonly ClientController clientController;
        private readonly SemaphoreSlim accountsSemaphore;


        public AccountManager(string accountsJson, ClientController clientController)
        {
            this.accountsJson = accountsJson;
            this.clientController = clientController;
            accountsSemaphore = new SemaphoreSlim(0, 1);
        }

        public async Task Save()
        {
            try
            {
                await accountsSemaphore.WaitAsync();
                await using var stream = File.OpenWrite(accountsJson);
                await JsonSerializer.SerializeAsync(stream, Accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
                await Task.Delay(1000);
                Logger.Debug("Accounts saved to {0}", accountsJson);
            }
            finally
            {
                accountsSemaphore.Release();
            }
        }

        public async Task Load()
        {
            try
            {
                await using var stream = File.OpenRead(accountsJson);
                Accounts = await JsonSerializer.DeserializeAsync<List<Account>>(stream);
                Logger.Debug("Accounts loaded from {0}", accountsJson);
            }
            finally
            {
                accountsSemaphore.Release();
            }
        }

        public async Task Launch(int maxInstances)
        {
            accountsSemaphore.Release(1);
            await Load();
            if (Accounts == null) return;
            var accountsToRun = Accounts.Where(a => a.LastSuccess < DateTime.UtcNow.Date);
            await clientController.Launch(this, maxInstances);
            await Save();
        }
    }
}
