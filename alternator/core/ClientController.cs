namespace guildwars2.tools.alternator;

public class ClientController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly LaunchType launchType;
    private readonly SemaphoreSlim loginSemaphore;
    private readonly DirectoryInfo applicationFolder;
    private readonly SettingsController settingsController;
    private readonly AuthenticationThrottle authenticationThrottle;
    private readonly VpnCollection vpnCollection;

    public event EventHandler? MetricsUpdated;


    public ClientController(
        DirectoryInfo applicationFolder,
        SettingsController settingsController,
        AuthenticationThrottle authenticationThrottle,
        VpnCollection vpnCollection,
        LaunchType launchType)
    {
        this.applicationFolder = applicationFolder;
        this.settingsController = settingsController;
        this.launchType = launchType;
        this.authenticationThrottle = authenticationThrottle;
        this.vpnCollection = vpnCollection;

        loginSemaphore = new SemaphoreSlim(0, 1);
    }

    public async Task LaunchMultiple(
        List<IAccount> selectedAccounts,
        AccountCollection accountCollection,
        bool all,
        bool ignoreVpn,
        int maxInstances,
        CancellationTokenSource cancellationTokenSource
        )
    {

        var accounts = selectedAccounts.Any() ? selectedAccounts : accountCollection.AccountsToRun(launchType, all);

        if (accounts == null || !accounts.Any())
        {
            Logger.Debug("No accounts to run.");
            return;
        }

        foreach (var account in accounts)
        {
            account.Done = false;
        }

        var vpnsUsed = new List<VpnDetails>();
        var clients = new List<Client>();
        var first = true;
        var start = DateTime.Now;
        try
        {
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            Logger.Debug("Max GW2 Instances={0}", maxInstances);

            var accountsByVpn = AccountCollection.AccountsByVpn(accounts, ignoreVpn);
            while (accountsByVpn.SelectMany(a => a.Value).Distinct().Any(a => !a.Done))
            {
                var now = DateTime.Now;

                var vpnSets = accountsByVpn
                    .Select(kv => new
                    {
                        Vpn = vpnCollection.GetVpn(kv.Key),
                        Accounts = kv.Value.Where(a => !a.Done).ToList()
                    })
                    .Where(s => s.Vpn != null)
                    .OrderByDescending(s => s.Vpn!.Available(now))
                    .ThenByDescending(s => s.Accounts.Count)
                    .ToList();

                Logger.Debug($"{vpnSets.Count} launch sets found");

                foreach (var vpnSet in vpnSets)
                {
                    var vpn = vpnSet.Vpn!;
                    var accountsToLaunch = vpnSet.Accounts
                        .Where(a => !a.Done)
                        .Take(settingsController.Settings!.VpnAccountCount)
                        .ToList();
                    if (!accountsToLaunch.Any()) continue;

                    var clientsToLaunch = new List<Client>();
                    foreach (var account in accountsToLaunch)
                    {
                        Logger.Debug($"Launching client for Account {account.Name}");
                        clientsToLaunch.Add(account.NewClient());
                    }
                    clients.AddRange(clientsToLaunch);

                    var waitUntil = vpn.Available(now).Subtract(now);
                    if (waitUntil.TotalSeconds > 0)
                    {
                        Logger.Debug($"VPN {vpn.Id} on login cooldown {waitUntil}");
                        await Task.Delay(waitUntil, cancellationTokenSource.Token);
                    }

                    try
                    {
                        if (!vpnsUsed.Contains(vpn)) vpnsUsed.Add(vpn);
                        var success = await vpn.Connect(cancellationTokenSource.Token);
                        if (!success)
                        {
                            Logger.Error($"VPN {vpn} Connection {vpn}");
                            continue;
                        }

                        Logger.Debug($"Launching {clientsToLaunch.Count} clients");
                        var tasks = PrimeLaunchTasks(vpn, clientsToLaunch, exeSemaphore, cancellationTokenSource.Token);
                        if (cancellationTokenSource.IsCancellationRequested) return;

                        if (first)
                        {
                            await Task.Delay(200, cancellationTokenSource.Token);
                            if (cancellationTokenSource.IsCancellationRequested) return;
                            // Release the hounds
                            exeSemaphore.Release(maxInstances);
                            loginSemaphore.Release(1);
                            first = false;
                        }

                        await Task.WhenAll(tasks.ToArray());
                    }
                    finally
                    {
                        var success = await vpn.Disconnect(cancellationTokenSource.Token);
                        if (!success)
                        {
                            Logger.Error($"VPN {vpn} Disconnection failed.");
                        }
                        else
                        {
                            authenticationThrottle.CurrentVpn = null;
                        }
                    }
                }
            }

            Logger.Info("All launch tasks finished.");
        }
        catch (Exception e)
        {
            Logger.Error(e, "LaunchMultiple: unexpected error");
        }
        finally
        {
            cancellationTokenSource.Cancel(true);
            await Restore(first);
            Logger.Info("GW2 account files restored.");
            await SaveMetrics(start, clients, vpnsUsed);
        }
    }

    private async Task SaveMetrics(DateTime startOfRun, List<Client> clients, List<VpnDetails> vpnDetailsList)
    {
        (string, DateTime) AddOffset(DateTime reference, DateTime time, string line)
        {
            if (time < reference) return (line + "\t", reference);
            line += $"\t{time.Subtract(reference).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";
            return (line, time);
        }

        var lines = new List<string>();

        foreach (var client in clients.OrderBy(c => c.StartAt))
        {
            var line = client.Account.Name;
            var reference = startOfRun;
            (line, reference) = AddOffset(reference, client.StartAt, line);
            (line, reference) = AddOffset(reference, client.AuthenticationAt, line);
            (line, reference) = AddOffset(reference, client.LoginAt, line);
            (line, reference) = AddOffset(reference, client.EnterAt, line);
            (line, reference) = AddOffset(reference, client.ExitAt, line);
            lines.Add(line);
        }

        foreach (var connection in vpnDetailsList
                     .Where(v => !string.IsNullOrEmpty(v.Id))
                     .SelectMany(v => v.Connections)
                     .Where(c => c.ConnectMetrics != null)
                     .OrderBy(c => c.ConnectMetrics!.StartAt))
        {
            var line = $"VPN-{connection.Id}";
            var reference = startOfRun;
            (line, reference) = AddOffset(reference, connection.ConnectMetrics!.StartAt, line);
            line += "\t";
            (line, reference) = AddOffset(reference, connection.ConnectMetrics!.FinishAt, line);
            if (connection.DisconnectMetrics != null)
            {
                (line, reference) = AddOffset(reference, connection.DisconnectMetrics!.StartAt, line);
                (line, reference) = AddOffset(reference, connection.DisconnectMetrics!.FinishAt, line);
            }
            lines.Add(line);
        }

        var metricsFile = settingsController.MetricsFile;
        await File.WriteAllLinesAsync(metricsFile, lines);
        var metricsFileUniquePath = settingsController.UniqueMetricsFile;
        File.Copy(metricsFile, metricsFileUniquePath);
        Logger.Info($"Metrics saved to {metricsFileUniquePath}");
        MetricsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private List<Task> PrimeLaunchTasks(VpnDetails vpnDetails, IEnumerable<Client> clients, SemaphoreSlim exeSemaphore,
        CancellationToken cancellationToken)
    {
        var tasks = clients.Select(client => Task.Run(async () =>
            {
                var launcher = new Launcher(client, launchType, applicationFolder, settingsController.Settings!, vpnDetails, cancellationToken);
                _ = await launcher.LaunchAsync(settingsController.DatFile!, applicationFolder, settingsController.GfxSettingsFile!, authenticationThrottle, loginSemaphore, exeSemaphore);
                LogManager.Flush();
            }, cancellationToken))
            .ToList();
        Logger.Debug("{0} launch tasks primed.", tasks.Count);
        return tasks;
    }

    private async Task Restore(bool first)
    {
        var obtainedLoginLock = false;
        if (!first)
        {
            Logger.Debug("{0} login semaphore={1}", nameof(Restore), loginSemaphore.CurrentCount);
            obtainedLoginLock = await loginSemaphore.WaitAsync(new TimeSpan(0, 2, 0));
            if (!obtainedLoginLock) Logger.Error("{0} login semaphore wait timed-out", nameof(Restore));
        }

        try
        {
            await Task.Run(() =>
            {
                SafeRestoreBackup(settingsController.DatFile!);
                SafeRestoreBackup(settingsController.GfxSettingsFile!);
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