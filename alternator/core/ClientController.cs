
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

        var vpnsUsed = new List<VpnDetails>();
        var first = true;
        var start = DateTime.Now;
        try
        {
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            Logger.Debug("Max GW2 Instances={0}", maxInstances);

            var clientsByVpn = AccountCollection.ClientsByVpn(accounts, ignoreVpn);
            while (clientsByVpn.SelectMany(c => c.Value).Distinct().Any(c => c.RunStatus != RunState.Completed))
            {
                var now = DateTime.Now;
                var vpnSets = clientsByVpn
                    .Select(kv => new
                    {
                        Vpn = vpnCollection.GetVpn(kv.Key),
                        Clients = kv.Value.Where(c => c.RunStatus != RunState.Completed).ToList()
                    })
                    .Where(s => s.Vpn != null)
                    .OrderByDescending(s => s.Vpn!.Available(now))
                    .ThenByDescending(s => s.Clients.Count)
                    .ToList();

                Logger.Debug($"{vpnSets.Count} launch sets found");

                foreach (var vpnSet in vpnSets)
                {
                    var vpn = vpnSet.Vpn!;
                    var clientsToLaunch = vpnSet.Clients
                        .Where(c => c.RunStatus != RunState.Completed)
                        .Take(settingsController.Settings!.VpnAccountCount)
                        .ToList();
                    if (!clientsToLaunch.Any()) continue;

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
            await SaveMetrics(start, accounts, vpnsUsed);
        }
    }

    private async Task SaveMetrics(DateTime startOfRun, List<IAccount> accounts, List<VpnDetails> vpnDetailsList)
    {
        var lines = new List<string>();

        string TimeOffset(DateTime reference, DateTime? time) => 
            time.HasValue && time.Value >= reference
            ? time.Value.Subtract(reference).TotalSeconds.ToString(CultureInfo.InvariantCulture) 
            : "";

        foreach (var client in accounts.Where(a => a.Client!=null).Select(a => a.Client).OrderBy(c => c!.StartAt))
        {
            var line = client!.Account.Name;
            line += $"\t{TimeOffset(startOfRun, client.StartAt)}";
            line += $"\t{TimeOffset(client.StartAt,client.AuthenticationAt)}";
            line += $"\t{TimeOffset(client.AuthenticationAt, client.LoginAt)}";
            line += $"\t{TimeOffset(client.LoginAt, client.EnterAt)}";
            line += $"\t{TimeOffset(client.EnterAt, client.ExitAt)}";
            lines.Add(line);
        }

        foreach (var vpn in vpnDetailsList.Where(v => !string.IsNullOrEmpty(v.Id)))
        {
            foreach (var connection in vpn.Connections.Where(c => c.ConnectMetrics != null))
            {
                var line = $"VPN-{vpn.Id}\t";
                line += $"\t{TimeOffset(startOfRun, connection.ConnectMetrics!.StartAt)}";
                line += $"\t{TimeOffset(connection.ConnectMetrics!.StartAt, connection.ConnectMetrics?.FinishAt)}";
                if (connection.DisconnectMetrics != null)
                {
                    line += $"\t{TimeOffset(connection.ConnectMetrics!.FinishAt, connection.DisconnectMetrics?.StartAt)}";
                    line += $"\t{TimeOffset(connection.DisconnectMetrics!.StartAt, connection.DisconnectMetrics?.FinishAt)}";
                }
                lines.Add(line);
            }
        }
        var timingsFilePath = Path.Combine(settingsController.SourceFolder.FullName, "gw2-alternator-metrics.txt");
        await File.WriteAllLinesAsync(timingsFilePath, lines);
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