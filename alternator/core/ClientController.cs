using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            var semaphore = new SemaphoreSlim(1, 1);
            foreach (var launcher in accounts.Select(account => new Launcher(account)))
            {
                launcher.Launch(loginFile, semaphore);
            }
        }
    }
}
