namespace guildwars2.tools.alternator;

public class Launcher
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Account account;
    private readonly LaunchType launchType;
    private readonly Settings settings;
    private readonly CancellationToken launchCancelled;
    private readonly FileInfo referenceGfxSettings;

    public Launcher(Account account, LaunchType launchType, DirectoryInfo applicationFolder, Settings settings, CancellationToken launchCancelled)
    {
        this.account = account;
        this.launchType = launchType;
        this.settings = settings;
        this.launchCancelled = launchCancelled;
        referenceGfxSettings = new FileInfo(Path.Combine(applicationFolder.FullName, "GW2 Custom GFX-Fastest.xml"));
    }

    public async Task<bool> Launch(
        FileInfo loginFile,
        FileInfo gfxSettingsFile,
        SemaphoreSlim loginSemaphore, 
        SemaphoreSlim exeSemaphore,
        int maxRetries,
        Counter launchCount)
    {
        var client = account.Client;
        try
        {
            int attempt = 0;
            bool loginInProcess = false;
            
            async Task? ReleaseLogin(int attemptCount, CancellationToken cancellationToken)
            {
                if (launchType is not LaunchType.Update && client.StartTime>DateTime.MinValue)
                {
                    var secondsSinceLogin = (DateTime.Now - client.StartTime).TotalSeconds;
                    Logger.Debug("{0} secondsSinceLogin={1}s", account.Name, secondsSinceLogin);
                    Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
                    var delay = LaunchDelay(launchCount.Count, attemptCount);
                    Logger.Debug("{0} minimum delay={1}s", account.Name, delay);
                    delay -= (int) secondsSinceLogin;
                    Logger.Debug("{0} actual delay={1}s", account.Name, delay);
                    if (delay > 0) await Task.Delay(new TimeSpan(0, 0, delay), cancellationToken);
                }

                loginSemaphore.Release();
                loginInProcess = false;
                Logger.Info("{0} login semaphore released, count={1}", account.Name, loginSemaphore.CurrentCount);
            }

            while (++attempt <= maxRetries)
            {
                Logger.Info("{0} login attempt={1}", account.Name, attempt);
                Logger.Info("{0} login semaphore entry, count={1}", account.Name, loginSemaphore.CurrentCount);
                client.RunStatus = RunState.WaitingForLoginSlot;
                await loginSemaphore.WaitAsync(launchCancelled);

                loginInProcess = true;

                Logger.Debug("{0} Login Free", account.Name);
                Task? releaseLoginTask = null;

                var exeInProcess = false;
                try
                {
                    await account.SwapFilesAsync(loginFile, gfxSettingsFile, referenceGfxSettings);

                    Task<bool> waitForExitTask;
                    try
                    {
                        Logger.Debug("{0} exe semaphore entry, count={1}", account.Name, exeSemaphore.CurrentCount);
                        client.RunStatus = RunState.WaitingForExeSlot;
                        await exeSemaphore.WaitAsync(launchCancelled);
                        exeInProcess = true;
                        launchCount.Increment();
                        if (!client.Start(launchType, settings.Gw2Folder))
                        {
                            Logger.Error("{0} exe start Failed", account.Name);
                            client.RunStatus = RunState.Error;
                            client.StatusMessage = "Exe start Failed";
                            continue;
                        }
                        Logger.Debug("{0} Login Finished", account.Name);
                        releaseLoginTask = ReleaseLogin(attempt, launchCancelled);
                        waitForExitTask = client.WaitForExit(launchType, launchCancelled);
                    }
                    finally
                    {
                        if (loginInProcess && releaseLoginTask == null) releaseLoginTask = ReleaseLogin(attempt, launchCancelled);
                    }

                    if (await waitForExitTask)
                    {
                        client.RunStatus = RunState.Completed;
                        account.LastLogin = DateTime.UtcNow;
                        if (launchType is LaunchType.Collect) account.LastCollection = DateTime.UtcNow;
                        return true;
                    }

                    Logger.Error("{0} exe Failed", account.Name);
                }
                finally
                {
                    if (loginInProcess && releaseLoginTask == null) _ = ReleaseLogin(attempt, launchCancelled);
                    Logger.Debug("{0} exe terminated", account.Name);
                    if (exeInProcess)
                    {
                        exeSemaphore.Release();
                        Logger.Debug("{0} exe semaphore released, count={1}", account.Name, exeSemaphore.CurrentCount);
                    }
                }
            }
            client.RunStatus = RunState.Error;
            client.StatusMessage = "Too many attempts";
            Logger.Error("{0} too many attempts, giving up", account.Name);
        }
        catch (OperationCanceledException)
        {
            client.RunStatus = RunState.Cancelled;
            Logger.Debug("{0} cancelled", account.Name);
        }
        catch (Exception e)
        {
            client.RunStatus = RunState.Error;
            client.StatusMessage = "Launch crashed";
            Logger.Error(e, "{0} launch crash", account.Name);
        }
        if (await client.Kill())
        {
            Logger.Error("{0} GW2 process killed", account.Name);
        }
        return false;
    }


    private int LaunchDelay(int count, int attempt)
    {
        if (attempt > 1) return 120 + 30 * (1 << (attempt - 1));

        if (count < 20) return 5;
        //if (count < 20) return 5 + (1 << (count - 2)) * 5;
        return 45;
        //return Math.Min(800, (300 + 10 * (count - 5)));

        // 0 | 5
        // 1 | 5
        // 2 | 10
        // 3 | 15
        // 4 | 25
        // 5 | 60
    }
}