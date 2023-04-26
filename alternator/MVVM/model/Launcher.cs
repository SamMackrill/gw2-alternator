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
        bool logAccount,
        AuthenticationThrottle authenticationThrottle,
        SemaphoreSlim loginSemaphore,
        SemaphoreSlim exeSemaphore)
    {
        if (!File.Exists(account.LoginFilePath)) return false;

        Task? releaseLoginTask = null;
        var loginInProcess = false;
        bool alsoFailVpn = false;

        client.RunStatusChanged += Client_RunStatusChanged;
        client.MutexDeleted += MutexDeleted;

        async Task? ReleaseLogin(CancellationToken cancellationToken)
        {
            Logger.Debug("{0} Release Login", account.Name);
            client.AccountLogger?.Debug("Release Login", account.Name);
            client.LaunchLogger?.Debug("{0} Release Login", account.Name);
            try
            {
                await authenticationThrottle.LoginDone(vpnDetails, client, launchType, cancellationToken);
            }
            finally
            {
                loginSemaphore.Release();
                Logger.Debug("{0} login semaphore released, count={1}", account.Name, loginSemaphore.CurrentCount);
                client.AccountLogger?.Debug("login semaphore released, count={1}", account.Name, loginSemaphore.CurrentCount);
                client.LaunchLogger?.Debug("{0} login semaphore released, count={1}", account.Name, loginSemaphore.CurrentCount);
            }
        }

        void MutexDeleted(object? sender, EventArgs e)
        {
            releaseLoginTask = ReleaseLogin(launchCancelled);
        }

        void ReleaseLoginIfRequired(bool logininprocess, ref Task? releaselogintask, CancellationToken launchcancelled)
        {
            if (!logininprocess) return;
            releaselogintask ??= ReleaseLogin(launchcancelled);
        }


        void Client_RunStatusChanged(object? sender, Client.ClientStateChangedEventArgs e)
        {
            Logger.Debug("{0} Status changed from {1} to {2}", account.Name, e.OldState, e.State);
            client.AccountLogger?.Debug("Status changed from {1} to {2}", account.Name, e.OldState, e.State);
            switch (e.State)
            {
                case RunStage.NotRun:
                    break;
                case RunStage.Started:
                    break;
                case RunStage.ReadyToLogin:
                    client.SendEnterKey(true, e.State.ToString());
                    break;
                case RunStage.LoginFailed:
                    Logger.Info("{0} login failed, giving up to try again", account.Name);
                    client.AccountLogger?.Debug("login failed, giving up to try again", account.Name);
                    authenticationThrottle.LoginFailed(vpnDetails, client, false);
                    ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
                    alsoFailVpn = false;
                    account.SetFail();
                    client.Kill().Wait();
                    break;
                case RunStage.LoginCrashed:
                    Logger.Info("{0} login crashed, giving up to try again", account.Name);
                    client.AccountLogger?.Debug("login crashed, giving up to try again", account.Name);
                    ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
                    alsoFailVpn = false;
                    account.SetFail();
                    client.Kill().Wait();
                    break;
                case RunStage.ReadyToPlay:
                    client.SendEnterKey(true, e.State.ToString());
                    break;
                case RunStage.Playing:
                    authenticationThrottle.LoginSucceeded(vpnDetails, client);
                    break;
                case RunStage.CharacterSelection:
                    client.SelectCharacter();
                    client.MinimiseWindow();
                    break;
                case RunStage.CharacterSelected:
                    client.StopSendingEnter();
                    if (launchType is LaunchType.Login) client.Shutdown(settings.ShutDownDelay).Wait();
                    break;
                case RunStage.EntryFailed:
                    Logger.Info("{0} entry failed, giving up to try again", account.Name);
                    client.AccountLogger?.Debug("entry failed, giving up to try again", account.Name);
                    ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
                    alsoFailVpn = false;
                    account.SetFail();
                    client.Kill().Wait();
                    break;
                case RunStage.WorldEntered:
                    if (launchType == LaunchType.Login)
                    {
                        client.Shutdown(settings.ShutDownDelay).Wait();
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

        loginInProcess = false;
        var exeInProcess = false;


        try
        {
            Logger.Info("{0} login attempt={1}", account.Name, account.Attempt);
            client.AccountLogger?.Debug("login attempt={1}", account.Name, account.Attempt);

            client.RunStatus = RunState.WaitingForLoginSlot;
            if (releaseLoginTask != null)
            {
                Logger.Debug("{0} login semaphore release, count={1}", account.Name, loginSemaphore.CurrentCount);
                client.AccountLogger?.Debug("login semaphore release, count={1}", account.Name, loginSemaphore.CurrentCount);
                await releaseLoginTask.WaitAsync(launchCancelled);
                releaseLoginTask = null;
            }
            Logger.Debug("{0} login semaphore entry, count={1}", account.Name, loginSemaphore.CurrentCount);
            client.AccountLogger?.Debug("login semaphore entry, count={1}", account.Name, loginSemaphore.CurrentCount);
            if (launchType == LaunchType.Login)
            {
                if (!await loginSemaphore.WaitAsync(new TimeSpan(0, 10, 0), launchCancelled)) throw new Gw2TimeoutException("Time-out waiting for Login Semaphore");
            }
            else
            {
                await loginSemaphore.WaitAsync(launchCancelled);
            }

            loginInProcess = true;
            Logger.Debug("{0} Login slot Free", account.Name);
            client.AccountLogger?.Debug("Login slot Free", account.Name);

            await account.SwapFilesAsync(loginFile, gfxSettingsFile, referenceGfxSettings);

            Logger.Debug("{0} exe semaphore entry, count={1}", account.Name, exeSemaphore.CurrentCount);
            client.AccountLogger?.Debug("exe semaphore entry, count={1}", account.Name, exeSemaphore.CurrentCount);
            client.RunStatus = RunState.WaitingForExeSlot;
            if (launchType == LaunchType.Login)
            {
                if (!await exeSemaphore.WaitAsync(new TimeSpan(0, 10, 0), launchCancelled)) throw new Gw2TimeoutException("Time-out waiting for Exe Semaphore");
            }
            else
            {
                await exeSemaphore.WaitAsync(launchCancelled);
            }
            exeInProcess = true;
            Logger.Debug("{0} Exe slot Free", account.Name);
            client.AccountLogger?.Debug("Exe slot Free", account.Name);

            // Ready to roll, have login and exe slot
            client.RunStatus = RunState.WaitingForAuthenticationThrottle;
            await authenticationThrottle.WaitAsync(client, vpnDetails, launchCancelled);

            await client.Launch(launchType, settings, shareArchive, logAccount, applicationFolder, vpnDetails, launchCancelled);

            client.RunStatus = RunState.Completed;
            vpnDetails.SetSuccess();
            account.Done = true;
            return true;
        }
        catch (OperationCanceledException ce)
        {
            client.RunStatus = RunState.Cancelled;
            Logger.Info("{0} Launch cancelled because {1}", account.Name, ce.CancellationToken.CancellationReason());
            client.AccountLogger?.Debug("Launch cancelled because {1}", account.Name, ce.CancellationToken.CancellationReason());
            alsoFailVpn = false;
        }
        catch (Gw2Exception e)
        {
            client.RunStatus = RunState.Error;
            client.StatusMessage = $"Launch failed: {e.Message}";
            Logger.Error(e, "{0} launch failed", account.Name);
            client.AccountLogger?.Debug(e, "launch failed", account.Name);
            alsoFailVpn = false && e is Gw2TimeoutException;
        }
        catch (Exception e)
        {
            client.RunStatus = RunState.Error;
            client.StatusMessage = "Launch crashed";
            Logger.Error(e, "{0} launch crash", account.Name);
            client.AccountLogger?.Debug(e, "launch crash", account.Name);
        }
        finally
        {
            ReleaseLoginIfRequired(loginInProcess, ref releaseLoginTask, launchCancelled);
            if (exeInProcess)
            {
                exeSemaphore.Release();
                Logger.Debug("{0} exe semaphore released, count={1}", account.Name, exeSemaphore.CurrentCount);
                client.AccountLogger?.Debug("exe semaphore released, count={1}", account.Name, exeSemaphore.CurrentCount);
            }
            client.RunStatusChanged -= Client_RunStatusChanged;
            client.ClearLogging();
        }

        if (await client.Kill())
        {
            Logger.Debug("{0} GW2 process killed", account.Name);
            client.AccountLogger?.Debug("GW2 process killed", account.Name);
        }

        //if (alsoFailVpn) vpnDetails.SetFail(client.Account);
        return false;
    }


}