using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using guildwars2.tools.alternator.core;
using NLog;

namespace guildwars2.tools.alternator
{
    public interface IClientController
    {
        Task Launch(AccountManager accountManager, int maxInstances);
    }

    public class ClientController : IClientController
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly FileInfo loginFile;

        public ClientController(FileInfo loginFile)
        {
            this.loginFile = loginFile;
        }

        public async Task Launch(AccountManager accountManager, int maxInstances)
        {
            if (!accountManager.Accounts.Any())
            {
                Logger.Debug("No accounts to run.");
                return;
            }
            var loginSemaphore = new SemaphoreSlim(0, 1);
            var accountsSemaphore = new SemaphoreSlim(0, 1);
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            var launchCount = new Counter();
            var tasks = accountManager.Accounts.Select(account => Task.Run(async () =>
                {
                    var launcher = new Launcher(account);
                    await launcher.Launch(loginFile, loginSemaphore, exeSemaphore, 3, launchCount);
                    LogManager.Flush();
                    try
                    {
                        await accountsSemaphore.WaitAsync();
                        await accountManager.Save();
                    }
                    finally
                    {
                        accountsSemaphore.Release();
                    }
                }))
                .ToList();
            Logger.Debug("{0} threads primed.", tasks.Count);
            // Allow all the tasks to start and block.
            await Task.Delay(200);

            // Release the hounds
            exeSemaphore.Release(maxInstances);
            loginSemaphore.Release(1);
            accountsSemaphore.Release(1);

            await Task.WhenAll(tasks.ToArray());

            Logger.Debug("All thread exited.");
        }
    }
}
