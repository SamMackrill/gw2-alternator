namespace guildwars2.tools.alternator;

public class Launcher
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Account account;
    private readonly LaunchType launchType;
    private readonly Settings settings;
    private readonly CancellationToken launchCancelled;
    private readonly FileInfo referenceGfxSettings;
    private readonly Client? client;

    public Launcher(Account account, LaunchType launchType, DirectoryInfo applicationFolder, Settings settings, CancellationToken launchCancelled)
    {
        this.account = account;
        this.launchType = launchType;
        this.settings = settings;
        this.launchCancelled = launchCancelled;
        referenceGfxSettings = new FileInfo(Path.Combine(applicationFolder.FullName, "GW2 Custom GFX-Fastest.xml"));
        client = account.Client;
    }

    public async Task<bool> LaunchAsync(
        FileInfo loginFile,
        FileInfo gfxSettingsFile,
        SemaphoreSlim loginSemaphore, 
        SemaphoreSlim exeSemaphore,
        int maxRetries,
        Counter launchCount)
    {
        if (client == null) return false;

        Task? releaseLoginTask = null;
        var loginInProcess = false;

        client.RunStatusChanged += Client_RunStatusChanged;

        async Task? ReleaseLogin(CancellationToken cancellationToken)
        {
            try
            {
                if (launchType is not LaunchType.Update && client.StartTime > DateTime.MinValue)
                {
                    var secondsSinceLogin = (DateTime.Now - client.StartTime).TotalSeconds;
                    Logger.Debug("{0} secondsSinceLogin={1}s", account.Name, secondsSinceLogin);
                    Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
                    var delay = LaunchDelay(launchCount.Count, client.Attempt);
                    Logger.Debug("{0} minimum delay={1}s", account.Name, delay);
                    delay -= (int)secondsSinceLogin;
                    Logger.Debug("{0} actual delay={1}s", account.Name, delay);
                    if (delay > 0) await Task.Delay(new TimeSpan(0, 0, delay), cancellationToken);
                }
            }
            finally
            {
                loginSemaphore.Release();
                Logger.Info("{0} login semaphore released, count={1}", account.Name, loginSemaphore.CurrentCount);
            }
        }


        void Client_RunStatusChanged(object? sender, ClientStateChangedEventArgs e)
        {
            Logger.Debug("{0} From {1} To {2}", account.Name, e.OldState, e.State);
            switch (e.State)
            {
                case RunStage.NotRun:
                    break;
                case RunStage.Started:
                    break;
                case RunStage.Authenticated:
                    ReleaseLoginIfRequired();
                    break;
                case RunStage.LoginFailed:
                    Logger.Info("{0} login failed, giving up to try again", account.Name);
                    ReleaseLoginIfRequired();
                    client?.Kill(false);
                    break;
                case RunStage.ReadyToPlay:
                    ReleaseLoginIfRequired();
                    client?.SendEnter();
                    break;
                case RunStage.Playing:
                    break;
                case RunStage.CharacterSelectReached:
                    client?.SelectCharacter();
                    break;
                case RunStage.CharacterSelected:
                    if (launchType == LaunchType.Login) client?.Kill(true);
                    break;
                case RunStage.EntryFailed:
                    Logger.Info("{0} entry failed, giving up to try again", account.Name);
                    ReleaseLoginIfRequired();
                    client?.Kill(false);
                    break;
                case RunStage.WorldEntered:
                    if (launchType == LaunchType.Login) client?.Kill(true);
                    break;
                case RunStage.Exited:
                    if (e.OldState != RunStage.CharacterSelected) break;
                    client.RunStatus = RunState.Completed;
                    account.LastLogin = DateTime.UtcNow;
                    if (launchType is LaunchType.Collect) account.LastCollection = DateTime.UtcNow;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unhandled RunStage: {e.State}");
            }
        }

        void ReleaseLoginIfRequired()
        {
            if (!loginInProcess) return;
            releaseLoginTask ??= ReleaseLogin(launchCancelled);
        }

        try
        {

            while (client.Attempt <= maxRetries)
            {
                loginInProcess = false;
                var exeInProcess = false;

                try
                {
                    Logger.Info("{0} login attempt={1}", account.Name, client.Attempt);

                    client.RunStatus = RunState.WaitingForLoginSlot;
                    if (releaseLoginTask != null)
                    {
                        Logger.Info("{0} login semaphore release, count={1}", account.Name, loginSemaphore.CurrentCount);
                        await releaseLoginTask.WaitAsync(launchCancelled);
                        releaseLoginTask = null;
                    }
                    Logger.Info("{0} login semaphore entry, count={1}", account.Name, loginSemaphore.CurrentCount);
                    await loginSemaphore.WaitAsync(launchCancelled);
                    loginInProcess = true;
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
                    ReleaseLoginIfRequired();
                    if (exeInProcess)
                    {
                        exeSemaphore.Release();
                        Logger.Debug("{0} exe semaphore released, count={1}", account.Name, exeSemaphore.CurrentCount);
                    }
                }

                if (await client.Kill(false))
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
        if (await client.Kill(false))
        {
            Logger.Debug("{0} GW2 process killed", account.Name);
        }
        return false;
    }


    private int LaunchDelay(int count, int attempt)
    {
        if (attempt > 1) return 60 + 30 * (1 << (attempt - 1));

        if (count < settings.AccountBand1) return settings.AccountBand1Delay;
        if (count < settings.AccountBand2) return settings.AccountBand2Delay;
        if (count < settings.AccountBand3) return settings.AccountBand3Delay;
        return settings.AccountBand3Delay + 60;
    }
}