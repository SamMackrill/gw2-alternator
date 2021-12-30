namespace guildwars2.tools.alternator;

public class Launcher
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Account account;
    private readonly LaunchType launchType;
    private readonly Settings settings;
    private readonly CancellationToken launchCancelled;
    private readonly FileInfo referenceGfxSettings;

    private Client? client;

    public Launcher(Account account, LaunchType launchType, DirectoryInfo applicationFolder, Settings settings, CancellationToken launchCancelled)
    {
        this.account = account;
        this.launchType = launchType;
        this.settings = settings;
        this.launchCancelled = launchCancelled;
        referenceGfxSettings = new FileInfo(Path.Combine(applicationFolder.FullName, "GW2 Custom GFX-Fastest.xml"));
        client = account.Client;
    }

    public async Task<bool> Launch(
        FileInfo loginFile,
        FileInfo gfxSettingsFile,
        SemaphoreSlim loginSemaphore, 
        SemaphoreSlim exeSemaphore,
        int maxRetries,
        Counter launchCount)
    {
        if (client == null) return false;

        int attempt = 0;
        Task? releaseLoginTask = null;

        client.Started += Client_Started;
        client.Authenticated += Client_Authenticated;
        client.ReadyToPlay += Client_ReadyToPlay;
        client.EnteredWorld += Client_EnteredWorld;
        client.ReadyToSelectCharactor += Client_ReadyToSelectCharactor;
        client.Exited += Client_Exited;

        async Task? ReleaseLogin(int attemptCount, CancellationToken cancellationToken)
        {
            if (launchType is not LaunchType.Update && client.StartTime > DateTime.MinValue)
            {
                var secondsSinceLogin = (DateTime.Now - client.StartTime).TotalSeconds;
                Logger.Debug("{0} secondsSinceLogin={1}s", account.Name, secondsSinceLogin);
                Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
                var delay = LaunchDelay(launchCount.Count, attemptCount);
                Logger.Debug("{0} minimum delay={1}s", account.Name, delay);
                delay -= (int)secondsSinceLogin;
                Logger.Debug("{0} actual delay={1}s", account.Name, delay);
                if (delay > 0) await Task.Delay(new TimeSpan(0, 0, delay), cancellationToken);
            }

            loginSemaphore.Release();
            Logger.Info("{0} login semaphore released, count={1}", account.Name, loginSemaphore.CurrentCount);
        }

        void Client_Started(object? sender, EventArgs e)
        {
            Logger.Debug("{0} Started", account.Name);
        }

        void Client_Authenticated(object? sender, EventArgs e)
        {
            Logger.Debug("{0} Authenticated", account.Name);
            ReleasaeLogin();
        }

        void Client_ReadyToPlay(object? sender, EventArgs e)
        {
            Logger.Debug("{0} Ready to Play", account.Name);
            ReleasaeLogin();
            client?.SendEnter();
        }

        void Client_ReadyToSelectCharactor(object? sender, EventArgs e)
        {     
            Logger.Debug("{0} Ready To Select Character", account.Name);
            client?.SendEnter();
        }

        void Client_EnteredWorld(object? sender, EventArgs e)
        {
            Logger.Debug("{0} Entered World", account.Name);
            if (launchType == LaunchType.Login) client?.Kill();
        }

        void Client_Exited(object? sender, EventArgs e)
        {
            Logger.Debug("{0} Exited", account.Name);
            if (client.RunStage == RunStage.WorldEntered)
            {
                client.RunStatus = RunState.Completed;
                account.LastLogin = DateTime.UtcNow;
                if (launchType is LaunchType.Collect) account.LastCollection = DateTime.UtcNow;
            }
        }

        void ReleasaeLogin()
        {
            releaseLoginTask ??= ReleaseLogin(attempt, launchCancelled);
        }

        try
        {

            while (++attempt <= maxRetries)
            {
                bool exeInProcess = false;

                try
                {
                    Logger.Info("{0} login attempt={1}", account.Name, attempt);

                    client.RunStatus = RunState.WaitingForLoginSlot;
                    Logger.Info("{0} login semaphore entry, count={1}", account.Name, loginSemaphore.CurrentCount);
                    await loginSemaphore.WaitAsync(launchCancelled);
                    Logger.Debug("{0} Login slot Free", account.Name);

                    await account.SwapFilesAsync(loginFile, gfxSettingsFile, referenceGfxSettings);

                    Logger.Debug("{0} exe semaphore entry, count={1}", account.Name, exeSemaphore.CurrentCount);
                    client.RunStatus = RunState.WaitingForExeSlot;
                    await exeSemaphore.WaitAsync(launchCancelled);
                    exeInProcess = true;
                    Logger.Debug("{0} Exe slot Free", account.Name);

                    launchCount.Increment();

                    await client.Launch(launchType, settings.Gw2Folder, launchCancelled);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    client.RunStatus = RunState.Cancelled;
                    Logger.Debug("{0} cancelled", account.Name);
                    return false;
                }
                catch (Gw2Exception e)
                {
                    client.RunStatus = RunState.Error;
                    client.StatusMessage = $"Launch failed: {e.Message}";
                    Logger.Error(e, "{0} launch failed", account.Name);
                }
                catch (Exception e)
                {
                    client.RunStatus = RunState.Error;
                    client.StatusMessage = "Launch crashed";
                    Logger.Error(e, "{0} launch crash", account.Name);
                }
                finally
                {
                    ReleasaeLogin();
                    if (exeInProcess)
                    {
                        exeSemaphore.Release();
                        Logger.Debug("{0} exe semaphore released, count={1}", account.Name, exeSemaphore.CurrentCount);
                    }
                }

                if (await client.Kill())
                {
                    Logger.Debug("{0} GW2 process killed", account.Name);
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
            Logger.Debug("{0} GW2 process killed", account.Name);
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