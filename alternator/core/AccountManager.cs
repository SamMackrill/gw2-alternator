using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using guildwars2.tools.alternator.core;

namespace guildwars2.tools.alternator
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
            accountsSemaphore = new SemaphoreSlim(1, 1);

            clientController.AfterLaunch += AfterLaunch;
        }

        private async void AfterLaunch(object? sender, GenericEventArgs<bool> e)
        {
            var account = sender as Account;
            //if (e.EventData) await Save();
        }

        public async Task Save()
        {
            try
            {
                Logger.Debug("Saving Accounts to {0}", accountsJson);
                await accountsSemaphore.WaitAsync();

                await using (var stream = File.OpenWrite(accountsJson))
                {
                    await JsonSerializer.SerializeAsync(stream, Accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
                }

                await using var stream2 = File.OpenWrite(accountsJson);
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
                await accountsSemaphore.WaitAsync();
                await using var stream = File.OpenRead(accountsJson);
                Accounts = await JsonSerializer.DeserializeAsync<List<Account>>(stream);
                Logger.Debug("Accounts loaded from {0}", accountsJson);
            }
            finally
            {
                accountsSemaphore.Release();
            }
        }

        public async Task Launch(int maxInstances, LaunchType launchType, CancellationToken launchCancelled)
        {
            await Load();

            var accountsToRun = AccountsToRun(launchType);
            if (accountsToRun == null) return;
            await clientController.Launch(accountsToRun, maxInstances, launchCancelled);

            await Save();
        }

        private List<Account>? AccountsToRun(LaunchType launchType)
        {
            if (Accounts == null) return null;
            return launchType switch
            {
                LaunchType.LaunchAll => Accounts,
                LaunchType.UpdateAll => Accounts,
                LaunchType.CollectAll => Accounts,
                LaunchType.LaunchNeeded => Accounts.Where(a => a.LastLogin < DateTime.UtcNow.Date).ToList(),
                LaunchType.CollectNeeded => Accounts.Where(a => a.LastCollection < DateTime.UtcNow.Date
                                                        && DateTime.UtcNow.Date.Subtract(a.LastCollection).TotalDays > 30
                                                        ).ToList(),
                _ => throw new ArgumentException(message: "invalid enum value", paramName: nameof(launchType))
            };
        }
    }
}
