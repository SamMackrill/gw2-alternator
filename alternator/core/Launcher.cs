using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using alternator.model;

namespace alternator.core
{
    public interface ILauncher
    {
        void Launch(FileInfo loginFile, SemaphoreSlim semaphore);
         Task LaunchAsync(FileInfo loginFile, SemaphoreSlim semaphore);
    }

    public class Launcher
    {
        private readonly Account account;

        public Launcher(Account account)
        {
            this.account = account;
        }

        public void Launch(FileInfo loginFile, SemaphoreSlim loginSemaphore, SemaphoreSlim exeSemaphore)
        {
            Debug.WriteLine($"{account.Name} login semaphore={loginSemaphore.CurrentCount}");
            loginSemaphore.Wait();
            Debug.WriteLine($"{account.Name} Login Free");
            Client client;
            try
            {
                try
                {
                    account.SwapLogin(loginFile);
                    client = new Client(account);
                    Debug.WriteLine($"{account.Name} exe semaphore={exeSemaphore.CurrentCount}");
                    exeSemaphore.Wait();
                    client.Start();
                }
                finally
                {
                    Debug.WriteLine($"{account.Name} Login Finished");
                    loginSemaphore.Release();
                }
                client.WaitForExit();
            }
            finally
            {
                Debug.WriteLine($"{account.Name} exe Finished");
                exeSemaphore.Release();
            }
        }

        public async Task LaunchAsync(FileInfo loginFile, SemaphoreSlim semaphore)
        {

            await semaphore.WaitAsync();
            Client client;
            try
            {
                await account.SwapLoginAsync(loginFile);
                client = new Client(account);
                await client.StartAsync();
            }
            finally
            {
                semaphore.Release();
            }

            client.WaitForExit();
        }
    }
}
