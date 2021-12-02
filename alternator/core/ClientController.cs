using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using alternator.model;

namespace alternator.core
{
    public interface IClientController
    {
        void Launch(List<Account> accounts);
    }

    public class ClientController : IClientController
    {
        private readonly FileInfo loginFile;

        public ClientController(FileInfo loginFile)
        {
            this.loginFile = loginFile;
        }

        public void Launch(List<Account> accounts)
        {
            var loginSemaphore = new SemaphoreSlim(0, 1);
            var exeSemaphore = new SemaphoreSlim(0, 3);
            var tasks = new List<Task>();
            foreach(var account in accounts)
            {
                var task = Task.Run(() =>
                {
                    var launcher = new Launcher(account);
                    launcher.Launch(loginFile, loginSemaphore, exeSemaphore);
                });
                tasks.Add(task);
            }
            // Allow all the tasks to start and block.
            Thread.Sleep(200);

            // Release the hounds
            exeSemaphore.Release(3);
            loginSemaphore.Release(1);

            Task.WaitAll(tasks.ToArray());

            Debug.WriteLine("Main thread exits.");

            Thread.Sleep(new TimeSpan(1, 0, 0));
        }
    }
}
