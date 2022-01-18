namespace guildwars2.tools.alternator;

public class ClientController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly FileInfo loginFile;
    private readonly FileInfo gfxSettingsFile;
    private readonly LaunchType launchType;
    private readonly SemaphoreSlim loginSemaphore;
    private readonly DirectoryInfo applicationFolder;
    private readonly Settings settings;
    private readonly AuthenticationThrottle authenticationThrottle;
    private readonly VPNCollection vpnCollection;

    public event EventHandler<GenericEventArgs<bool>>? AfterLaunchAccount;

    public ClientController(DirectoryInfo applicationFolder, SettingsController settingsController,
        AuthenticationThrottle authenticationThrottle, VPNCollection vpnCollection, LaunchType launchType)
    {
        this.applicationFolder = applicationFolder;
        settings = settingsController.Settings!;
        loginFile = settingsController.DatFile!;
        gfxSettingsFile = settingsController.GfxSettingsFile!;
        this.launchType = launchType;
        this.authenticationThrottle = authenticationThrottle;
        this.vpnCollection = vpnCollection;

        loginSemaphore = new SemaphoreSlim(0, 1);
    }

    public async Task LaunchMultiple(List<IAccount> selectedAccounts, AccountCollection accountCollection, bool all, int maxInstances, CancellationTokenSource cancellationTokenSource)
    {

        var accounts = selectedAccounts.Any() ? selectedAccounts : accountCollection.AccountsToRun(launchType, all);

        if (accounts == null || !accounts.Any())
        {
            Logger.Debug("No accounts to run.");
            return;
        }

        try
        {

            var accountsByVpn = AccountCollection.SplitByVpn(accounts);

            //var vpnAccounts = accounts.Where(a => a.VPN != null && a.VPN.Contains("NY")).ToList();

            //var vpn = vpnCollection.VPN.FirstOrDefault(v => v.Id == "NY");

            //var vpnProcess = Process.Start("rasdial", vpn.ConnectionName);
            //vpnProcess.Start();
            //await vpnProcess.WaitForExitAsync(cancellationTokenSource.Token);

            //if (vpnProcess.ExitCode !=) throw new InvalidOperationException($"Cound not connect to VPN {vpn}");

            //accountsToRun = accountsToRun.Where(a => a.Name == "Fish35").ToList();

            Logger.Debug("Max GW2 Instances={0}", maxInstances);
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            var tasks = accounts.Select(account => Task.Run(async () =>
                {
                    var launcher = new Launcher(account, launchType, applicationFolder, settings, cancellationTokenSource.Token);
                    var success = await launcher.LaunchAsync(loginFile, applicationFolder, gfxSettingsFile, authenticationThrottle, loginSemaphore, exeSemaphore, 3);
                    AfterLaunchAccount?.Invoke(account, new GenericEventArgs<bool>(success));
                    LogManager.Flush();
                }, cancellationTokenSource.Token))
                .ToList();
            Logger.Debug("{0} launch tasks primed.", tasks.Count);
            // Allow all the tasks to start and block.
            await Task.Delay(200, cancellationTokenSource.Token);
            if (cancellationTokenSource.IsCancellationRequested) return;

            // Release the hounds
            exeSemaphore.Release(maxInstances);
            loginSemaphore.Release(1);

            await Task.WhenAll(tasks.ToArray());
            Logger.Info("All launch tasks finished.");
        }
        finally
        {
            cancellationTokenSource.Cancel(true);
            await Restore();
            Logger.Info("GW2 account files restored.");
        }
    }

    private async Task Restore()
    {
        Logger.Info("{0} login semaphore={1}", nameof(Restore), loginSemaphore.CurrentCount);
        var obtainedLoginLock = await loginSemaphore.WaitAsync(new TimeSpan(0, 2, 0));
        if (!obtainedLoginLock) Logger.Error("{0} login semaphore wait timed-out", nameof(Restore));
        try
        {
            await Task.Run(() =>
            {
                SafeRestoreBackup(loginFile);
                SafeRestoreBackup(gfxSettingsFile);
            });
        }
        finally
        {
            if (obtainedLoginLock) loginSemaphore.Release();
        }
    }

    private void SafeRestoreBackup(FileInfo file)
    {
        var backup = new FileInfo($"{file.FullName}.bak");
        if (!backup.Exists) return;
        try
        {
            file.Delete(); // Symbolic links need to be specifically deleted
            backup.MoveTo(file.FullName);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Could not restore {file} from backup!");
        }
    }
}