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
        void Launch(IEnumerable<Account> accounts);
    }

    public class ClientController : IClientController
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly FileInfo loginFile;

        public ClientController(FileInfo loginFile)
        {
            this.loginFile = loginFile;
        }

        public void Launch(IEnumerable<Account> accounts)
        {
            var loginSemaphore = new SemaphoreSlim(0, 1);
            var exeSemaphore = new SemaphoreSlim(0, 1);
            int launchCount = 0;
            var tasks = accounts.Select(account => Task.Run(() =>
                {
                    var launcher = new Launcher(account);
                    launcher.Launch(loginFile, loginSemaphore, exeSemaphore, ref launchCount);
                }))
                .ToList();
            Logger.Debug("{0} threads primed.", tasks.Count);
            // Allow all the tasks to start and block.
            Thread.Sleep(200);

            // Release the hounds
            exeSemaphore.Release(1);
            loginSemaphore.Release(1);

            Task.WaitAll(tasks.ToArray());

            Logger.Debug("All thread exited.");
        }
    }
}
