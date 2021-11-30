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
    public interface ILauncher
    {
         void Launch(FileInfo loginFile, SemaphoreSlim semaphore);
    }

    public class Launcher : ILauncher
    {
        private readonly Account account;

        public Launcher(Account account)
        {
            this.account = account;
        }

        public async void Launch(FileInfo loginFile, SemaphoreSlim semaphore)
        {

            await semaphore.WaitAsync();
            try
            {
                await account.SwapLogin(loginFile);
                var client = new Client(account);
                await client.Start();
            }
            finally
            {
                semaphore.Release();
            }

            // Wait for login
            // Auto 
        }
    }
}
