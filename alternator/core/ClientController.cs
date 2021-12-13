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
    public class ClientController
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly FileInfo loginFile;
        private readonly LaunchType launchType;

        public event EventHandler<GenericEventArgs<bool>>? AfterLaunch;

        public ClientController(FileInfo loginFile, LaunchType launchType)
        {
            this.loginFile = loginFile;
            this.launchType = launchType;
        }

        public async Task Launch(List<Account> accounts, int maxInstances, CancellationToken launchCancelled)
        {
            if (!accounts.Any())
            {
                Logger.Debug("No accounts to run.");
                return;
            }
            Logger.Debug("Max GW2 Instances={0}", maxInstances);
            var loginSemaphore = new SemaphoreSlim(0, 1);
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            var launchCount = new Counter();
            var tasks = accounts.Select(account => Task.Run(async () =>
                {
                    var launcher = new Launcher(account, launchType, launchCancelled);
                    var success = await launcher.Launch(loginFile, loginSemaphore, exeSemaphore, 3, launchCount);
                    AfterLaunch?.Invoke(account, new GenericEventArgs<bool>(success));
                    LogManager.Flush();
                }, launchCancelled))
                .ToList();
            Logger.Debug("{0} threads primed.", tasks.Count);
            // Allow all the tasks to start and block.
            await Task.Delay(200, launchCancelled);
            if (launchCancelled.IsCancellationRequested) return;

            // Release the hounds
            exeSemaphore.Release(maxInstances);
            loginSemaphore.Release(1);

            await Task.WhenAll(tasks.ToArray());

            Logger.Debug("All thread exited.");
        }
    }
}
