using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace guildwars2.tools.alternator
{
    public interface IClientController
    {
        Task Launch(IEnumerable<Account> accounts);
    }

    public class ClientController : IClientController
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly FileInfo loginFile;

        public ClientController(FileInfo loginFile)
        {
            this.loginFile = loginFile;
        }

        private const int maxInstances = 1;

        public async Task Launch(IEnumerable<Account> accounts)
        {
            if (!accounts.Any())
            {
                Logger.Debug("No accounts to run.");
                return;
            }
            var loginSemaphore = new SemaphoreSlim(0, 1);
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            var launchCount = new Counter();
            var tasks = accounts.Select(account => Task.Run(async () =>
                {
                    var launcher = new Launcher(account);
                    await launcher.Launch(loginFile, loginSemaphore, exeSemaphore, 3, launchCount);
                }))
                .ToList();
            Logger.Debug("{0} threads primed.", tasks.Count);
            // Allow all the tasks to start and block.
            await Task.Delay(200);

            // Release the hounds
            exeSemaphore.Release(maxInstances);
            loginSemaphore.Release(1);

            await Task.WhenAll(tasks.ToArray());

            Logger.Debug("All thread exited.");
        }
    }
}
