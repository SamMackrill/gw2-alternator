using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace guildwars2.tools.alternator
{
    public interface ILauncher
    {
        void Launch(FileInfo loginFile, SemaphoreSlim semaphore);
         Task LaunchAsync(FileInfo loginFile, SemaphoreSlim semaphore);
    }

    public class Launcher
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly Account account;

        public Launcher(Account account)
        {
            this.account = account;
        }

        public void Launch(FileInfo loginFile, SemaphoreSlim loginSemaphore, SemaphoreSlim exeSemaphore, ref int launchCount)
        {
            Logger.Debug("{0} login semaphore={1}", account.Name, loginSemaphore.CurrentCount);
            loginSemaphore.Wait();
            Logger.Debug("{0} Login Free", account.Name);
            try
            {
                Client client;
                try
                {
                    account.SwapLogin(loginFile);
                    client = new Client(account);
                    Logger.Debug("{0} login semaphore={1}", account.Name, loginSemaphore.CurrentCount);
                    exeSemaphore.Wait();
                    int delay = LaunchDelay(++launchCount);
                    Logger.Debug("{0} delay={1}s", account.Name, delay);
                    Task.Delay(new TimeSpan(0, 0, delay));
                    client.Start();
                }
                finally
                {
                    Logger.Debug("{0} Login Finished", account.Name);
                    loginSemaphore.Release();
                }

                if (client.WaitForExit())
                {
                    account.LastSuccess = DateTime.UtcNow;
                }
                else
                {
                    Logger.Debug("{0} exe Failed", account.Name);
                    //exeSemaphore.Release();
                }
            }
            finally
            {
                Logger.Debug("{0} exe Finished", account.Name);
                exeSemaphore.Release();
            }
        }

        private int LaunchDelay(int count)
        {
            if (count <= 1) return 5;
            if (count < 5) return (1 << (count - 2)) * 10;

            return Math.Min(800, (300 + 20 * (count - 5)));
        }

        //public async Task LaunchAsync(FileInfo loginFile, SemaphoreSlim semaphore)
        //{

        //    await semaphore.WaitAsync();
        //    Client client;
        //    try
        //    {
        //        await account.SwapLoginAsync(loginFile);
        //        client = new Client(account);
        //        await client.StartAsync();
        //    }
        //    finally
        //    {
        //        semaphore.Release();
        //    }

        //    client.WaitForExit();
        //}
    }
}
