namespace guildwars2.tools.alternator;

public class Launcher
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string Gw2Location = @"G:\Games\gw2";

    private readonly Account account;
    private readonly LaunchType launchType;
    private readonly CancellationToken launchCancelled;
    private readonly FileInfo referenceGfxSettings;

    public Launcher(Account account, LaunchType launchType, CancellationToken launchCancelled)
    {
        this.account = account;
        this.launchType = launchType;
        this.launchCancelled = launchCancelled;
        referenceGfxSettings = new FileInfo(Path.Combine(Gw2Location, "GW2 Custom GFX-Fastest.xml"));
    }

    public async Task<bool> Launch(
        FileInfo loginFile,
        FileInfo gfxSettingsFile,
        SemaphoreSlim loginSemaphore, 
        SemaphoreSlim exeSemaphore,
        int maxRetries,
        Func<bool> lastLaunch,
        Counter launchCount)
    {
        try
        {
            int attempt = 0;
            
            async Task? ReleaseLogin(int attemptCount, CancellationToken cancellationToken)
            {
                if (launchType is not LaunchType.Update)
                {
                    var secondsSinceLogin = (DateTime.Now - account.Client.StartTime).TotalSeconds;
                    Logger.Debug("{0} secondsSinceLogin={1}s", account.Name, secondsSinceLogin);
                    Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
                    var delay = LaunchDelay(launchCount.Count, attemptCount);
                    Logger.Debug("{0} minimum delay={1}s", account.Name, delay);
                    delay -= (int) secondsSinceLogin;
                    Logger.Debug("{0} actual delay={1}s", account.Name, delay);
                    if (delay > 0) await Task.Delay(new TimeSpan(0, 0, delay), cancellationToken);
                }

                loginSemaphore.Release();
                Logger.Debug("{0} loginSemaphore released", account.Name);
            }

            while (++attempt <= maxRetries)
            {
                Logger.Debug("{0} login semaphore={1}", account.Name, loginSemaphore.CurrentCount);
                account.Client.RunStatus = RunState.Waiting;
                await loginSemaphore.WaitAsync(launchCancelled);

                Logger.Debug("{0} Login Free", account.Name);
                Task? releaseLoginTask = null;
                try
                {
                    await account.SwapFilesAsync(loginFile, gfxSettingsFile, referenceGfxSettings);
                    Task<bool> waitForExitTask;
                    try
                    {
                        Logger.Debug("{0} login semaphore={1}", account.Name, loginSemaphore.CurrentCount);
                        account.Client.RunStatus = RunState.Waiting;
                        await exeSemaphore.WaitAsync(launchCancelled);
                        launchCount.Increment();
                        if (!account.Client.Start(launchType))
                        {
                            Logger.Error("{0} exe start Failed", account.Name);
                            account.Client.RunStatus = RunState.Error;
                            continue;
                        }
                        Logger.Debug("{0} Login Finished", account.Name);
                        waitForExitTask = account.Client.WaitForExit(launchType, launchCancelled);
                    }
                    finally
                    {
                        if (releaseLoginTask != null && lastLaunch?.Invoke() != true) await releaseLoginTask;
                        releaseLoginTask = ReleaseLogin(attempt, launchCancelled);
                    }

                    if (await waitForExitTask)
                    {
                        account.LastLogin = DateTime.UtcNow;
                        if (launchType is LaunchType.Collect) account.LastCollection = DateTime.UtcNow;
                        return true;
                    }

                    account.Client.RunStatus = RunState.Error;
                    Logger.Error("{0} exe Failed", account.Name);
                }
                finally
                {
                    if (releaseLoginTask != null) await releaseLoginTask;
                    Logger.Debug("{0} exe terminated", account.Name);
                    exeSemaphore.Release();
                }
            }
            account.Client.RunStatus = RunState.Error;
            Logger.Error("{0} too many attempts, giving up", account.Name);
        }
        catch (OperationCanceledException)
        {
            account.Client.RunStatus = RunState.Cancelled;
            Logger.Debug("{0} cancelled", account.Name);
        }
        catch (Exception e)
        {
            account.Client.RunStatus = RunState.Error;
            Logger.Error(e, "{0} launch failed", account.Name);
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