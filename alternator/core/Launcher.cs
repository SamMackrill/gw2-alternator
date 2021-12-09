using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace guildwars2.tools.alternator
{
    public class Launcher
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly Account account;

        public Launcher(Account account)
        {
            this.account = account;
        }

        public async Task<bool> Launch(FileInfo loginFile, SemaphoreSlim loginSemaphore, SemaphoreSlim exeSemaphore, int maxRetries, Counter launchCount)
        {
            int attempt = 0;
            var client = new Client(account);

            async Task? ReleaseLogin(int attemptCount)
            {
                var secondsSinceLogin = (DateTime.Now - client.StartedTime).TotalSeconds;
                Logger.Debug("{0} secondsSinceLogin={1}s", account.Name, secondsSinceLogin);
                Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
                var delay = LaunchDelay(launchCount.Count, attemptCount);
                Logger.Debug("{0} minimum delay={1}s", account.Name, delay);
                delay -= (int)secondsSinceLogin;
                Logger.Debug("{0} actual delay={1}s", account.Name, delay);
                if (delay > 0) await Task.Delay(new TimeSpan(0, 0, delay));
                loginSemaphore.Release();
                Logger.Debug("{0} loginSemaphore released", account.Name);
            }

            while (++attempt <= maxRetries)
            {
                Logger.Debug("{0} login semaphore={1}", account.Name, loginSemaphore.CurrentCount);
                await loginSemaphore.WaitAsync();
                Logger.Debug("{0} Login Free", account.Name);
                Task? releaseLoginTask = null;
                try
                {
                    await account.SwapLoginAsync(loginFile);
                    Task<bool> waitForExitTask;
                    try
                    {
                        Logger.Debug("{0} login semaphore={1}", account.Name, loginSemaphore.CurrentCount);
                        await exeSemaphore.WaitAsync();
                        launchCount.Increment();
                        if (!client.Start())
                        {
                            Logger.Debug("{0} exe start Failed", account.Name);
                            continue;
                        }
                        Logger.Debug("{0} Login Finished", account.Name);
                        waitForExitTask = client.WaitForExit();
                    }
                    finally
                    {
                        if (releaseLoginTask != null) await releaseLoginTask;
                        releaseLoginTask = ReleaseLogin(attempt);
                    }

                    if (await waitForExitTask)
                    {
                        account.LastSuccess = DateTime.UtcNow;
                        return true;
                    }

                    Logger.Debug("{0} exe Failed", account.Name);
                }
                finally
                {
                    if (releaseLoginTask != null) await releaseLoginTask;
                    Logger.Debug("{0} exe terminated", account.Name);
                    exeSemaphore.Release();
                }
            }
            Logger.Debug("{0} too many attempts, giving up", account.Name);
            return false;
        }


        private int LaunchDelay(int count, int attempt)
        {
            if (attempt > 1) return 300;

            if (count <= 1) return 5;
            if (count < 5) return 5 + (1 << (count - 2)) * 5;
            return 60;
            //return Math.Min(800, (300 + 10 * (count - 5)));

            // 0 | 5
            // 1 | 5
            // 2 | 10
            // 3 | 15
            // 4 | 25
            // 5 | 60
        }
    }
}
