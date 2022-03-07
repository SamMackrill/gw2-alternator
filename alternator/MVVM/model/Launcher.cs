namespace guildwars2.tools.alternator;

public class Launcher
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IAccount account;
    private readonly LaunchType launchType;
    private readonly Settings settings;
    private readonly VpnDetails vpnDetails;
    private readonly CancellationToken launchCancelled;
    private readonly FileInfo referenceGfxSettings;
    private readonly Client client;

    public event EventHandler<EventArgs>? ClientReady;
    public event EventHandler<EventArgs>? ClientClosed;


    public Launcher(
        Client client, 
        LaunchType launchType, 
        DirectoryInfo applicationFolder, 
        Settings settings,
        VpnDetails vpnDetails, 
        CancellationToken launchCancelled)
    {
        this.client = client;
        this.launchType = launchType;
        this.settings = settings;
        this.vpnDetails = vpnDetails;
        this.launchCancelled = launchCancelled;
        referenceGfxSettings = new FileInfo(Path.Combine(applicationFolder.FullName, "GW2 Custom GFX-Fastest.xml"));

        account = client.Account;
    }


    public async Task<bool> LaunchAsync(
        FileInfo loginFile,
        DirectoryInfo applicationFolder,
        FileInfo gfxSettingsFile,
        bool shareArchive,
        AuthenticationThrottle authenticationThrottle,
        SemaphoreSlim loginSemaphore,
        SemaphoreSlim exeSemaphore)
    {
        Task? releaseLoginTask = null;
        var loginInProcess = false;

        client.RunStatusChanged += Client_RunStatusChanged;

        async Task? ReleaseLogin(CancellationToken cancellationToken)
        {
            Logger.Debug("{0} login semaphore release", account.Name);
            try
            {
                await authenticationThrottle.LoginDone(vpnDetails, client, launchType, cancellationToken);
            }
            finally
            {
                loginSemaphore.Release();
                Logger.Debug("{0} login semaphore released, count={1}", account.Name, loginSemaphore.CurrentCount);
            }
        }


        void Client_RunStatusChanged(object? sender, Client.ClientStateChangedEventArgs e)
        {
            Logger.Debug("{0} Status changed from {1} to {2}", account.Name, e.OldState, e.State);
            switch (e.State)
            {
                case RunStage.NotRun:
                    break;
                case RunStage.Started:
                    break;
                case RunStage.Authenticated:
                    ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
                    break;
                case RunStage.LoginFailed:
                    Logger.Info("{0} login failed, giving up to try again", account.Name);
                    authenticationThrottle.LoginFailed(vpnDetails, client);
                    ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
                    account.SetFail();
                    client.Kill().Wait();
                    break;
                case RunStage.ReadyToPlay:
                    ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
                    client.SendEnter();
                    break;
                case RunStage.Playing:
                    authenticationThrottle.LoginSucceeded(vpnDetails, client);
                    break;
                case RunStage.CharacterSelectReached:
                    client.SelectCharacter();
                    client.MinimiseWindow();
                    break;
                case RunStage.CharacterSelected:
                    if (launchType is LaunchType.Login) client.Shutdown().Wait();
                    break;
                case RunStage.EntryFailed:
                    Logger.Info("{0} entry failed, giving up to try again", account.Name);
                    ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
                    account.SetFail();
                    client.Kill().Wait();
                    break;
                case RunStage.WorldEntered:
                    if (launchType == LaunchType.Login)
                    {
                        client.Shutdown().Wait();
                        return;
                    }
                    ClientReady?.Invoke(client, EventArgs.Empty);
                    break;
                case RunStage.Exited:
                    try
                    {
                        if (e.OldState is not RunStage.CharacterSelected and not RunStage.WorldEntered) break;
                        switch (launchType)
                        {
                            case LaunchType.Login:
                                if (account.LoginRequired) account.LoginCount++;
                                break;
                            case LaunchType.Collect:
                                account.SetCollected();
                                break;
                        }
                        account.SetLogin();
                    }
                    finally
                    {
                        ClientClosed?.Invoke(client, EventArgs.Empty);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unhandled RunStage: {e.State}");
            }
        }

        void ReleaseLoginIfRequired(bool logininprocess, ref Task? releaselogintask, CancellationToken launchcancelled)
        {
            if (!logininprocess) return;
            releaselogintask ??= ReleaseLogin(launchcancelled);
        }

        loginInProcess = false;
        var exeInProcess = false;

        try
        {
            Logger.Info("{0} login attempt={1}", account.Name, account.Attempt);

            client.RunStatus = RunState.WaitingForLoginSlot;
            if (releaseLoginTask != null)
            {
                Logger.Debug("{0} login semaphore release, count={1}", account.Name, loginSemaphore.CurrentCount);
                await releaseLoginTask.WaitAsync(launchCancelled);
                releaseLoginTask = null;
            }
            Logger.Debug("{0} login semaphore entry, count={1}", account.Name, loginSemaphore.CurrentCount);
            if (!await loginSemaphore.WaitAsync(new TimeSpan(0, 10, 0), launchCancelled)) throw new Gw2Exception("Time-out waiting for Login Semaphore");
            loginInProcess = true;
            Logger.Debug("{0} Login slot Free", account.Name);

            await account.SwapFilesAsync(loginFile, gfxSettingsFile, referenceGfxSettings);

            Logger.Debug("{0} exe semaphore entry, count={1}", account.Name, exeSemaphore.CurrentCount);
            client.RunStatus = RunState.WaitingForExeSlot;
            if (!await exeSemaphore.WaitAsync(new TimeSpan(0, 10, 0), launchCancelled)) throw new Gw2Exception("Time-out waiting for Exe Semaphore");
            exeInProcess = true;
            Logger.Debug("{0} Exe slot Free", account.Name);

            // Ready to roll, have login and exe slot
            client.RunStatus = RunState.WaitingForAuthenticationThrottle;
            await authenticationThrottle.WaitAsync(client, vpnDetails, launchCancelled);

            await client.Launch(launchType, settings, shareArchive, applicationFolder, launchCancelled);

            client.RunStatus = RunState.Completed;
            vpnDetails.SetSuccess();
            account.Done = true;
            return true;
        }
        catch (OperationCanceledException)
        {
            client.RunStatus = RunState.Cancelled;
            Logger.Info("{0} cancelled", account.Name);
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
            ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
            if (exeInProcess)
            {
                exeSemaphore.Release();
                Logger.Debug("{0} exe semaphore released, count={1}", account.Name, exeSemaphore.CurrentCount);
            }
            client.RunStatusChanged -= Client_RunStatusChanged;
        }

        if (await client.Kill())
        {
            Logger.Debug("{0} GW2 process killed", account.Name);
        }

        if (client.RunStatus != RunState.Cancelled) vpnDetails.SetFail(client.Account);
        return false;
    }

}