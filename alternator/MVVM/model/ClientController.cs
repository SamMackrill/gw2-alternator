﻿namespace guildwars2.tools.alternator;

public class ClientController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly LaunchType launchType;
    private readonly SemaphoreSlim loginSemaphore;
    private readonly DirectoryInfo applicationFolder;
    private readonly ISettingsController settingsController;
    private readonly AuthenticationThrottle authenticationThrottle;
    private readonly IVpnCollection vpnCollection;

    public event EventHandler? MetricsUpdated;


    public ClientController(
        DirectoryInfo applicationFolder,
        ISettingsController settingsController,
        AuthenticationThrottle authenticationThrottle,
        IVpnCollection vpnCollection,
        LaunchType launchType)
    {
        this.applicationFolder = applicationFolder;
        this.settingsController = settingsController;
        this.launchType = launchType;
        this.authenticationThrottle = authenticationThrottle;
        this.vpnCollection = vpnCollection;

        readyClients = new List<Client>();
        loginSemaphore = new SemaphoreSlim(0, 1);
    }

    private record VpnAccounts(VpnDetails Vpn, List<IAccount> Accounts);

    public async Task LaunchMultiple(
        List<IAccount> selectedAccounts,
        IAccountCollection accountCollection,
        bool all,
        bool ignoreVpn,
        int maxInstances,
        CancellationTokenSource cancellationTokenSource
        )
    {
        readyClients.Clear();
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
            while (accounts.Any(a => !a.Done))
            {
                var now = DateTime.Now;

                var accountsByVpnDetails = accountsByVpn
                    .Select(kv => new VpnAccounts(vpnCollection.GetVpn(kv.Key), kv.Value.Where(a => !a.Done).ToList()))
                    .ToList();

                AddNonVpnAccounts(accountsByVpnDetails, accounts);

                var vpnSets = accountsByVpnDetails
                    .OrderBy(s => s.Vpn.Available(now))
                    .ThenBy(s => s.Vpn.IsReal ? 1 : 0)
                    .ThenByDescending(s => s.Accounts.Count)
                    .ToList();

                Logger.Debug($"{vpnSets.Count} launch sets found");

                foreach (var (vpn, vpnAccounts) in vpnSets)
                {
                    var accountsToLaunch = vpnAccounts
                        .Where(a => !a.Done)
                        .Take(settingsController.Settings!.VpnAccountCount)
                        .ToList();
                    if (!accountsToLaunch.Any()) continue;

                    var clientsToLaunch = new List<Client>();
                    foreach (var account in accountsToLaunch)
                    {
                        Logger.Debug($"Launching client for Account {account.Name}");
                        clientsToLaunch.Add(await account.NewClient());
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
                        var status = await vpn.Connect(cancellationTokenSource.Token);
                        if (status != null)
                        {
                            Logger.Error($"VPN {vpn} Connection {vpn} : {status}");
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
                        var status = await vpn.Disconnect(cancellationTokenSource.Token);
                        if (status != null)
                        {
                            Logger.Error($"VPN {vpn} Disconnection failed : {status}");
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

    private void AddNonVpnAccounts(List<VpnAccounts> accountsByVpnDetails, List<IAccount> accounts)
    {
        var nonVpnAccounts = accountsByVpnDetails.FirstOrDefault(a => !a.Vpn.IsReal);
        
        var topUpCount = settingsController.Settings!.VpnAccountCount;
        if (nonVpnAccounts != null) topUpCount -= nonVpnAccounts.Accounts.Count(a => !a.Done);
        if (topUpCount <= 0) return;

        var topUpAccounts = accounts.Where(a => !a.Done).Take(topUpCount).ToList();

        if (nonVpnAccounts == null)
        {
            accountsByVpnDetails.Add(new VpnAccounts(vpnCollection.GetVpn(""), topUpAccounts));
        }
        else
        {
            nonVpnAccounts.Accounts.AddRange(topUpAccounts);
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

        var lines = new List<string>
        {
            $"Started\t{startOfRun:d}\t{startOfRun:T}", 
            $"Total Time\t{DateTime.Now.Subtract(startOfRun).TotalSeconds}\ts",
            "Account\tStart\tAuthenticate\tLogin\tEnter\tExit",
        };

        foreach (var client in clients.Where(c => c.Account.Name != null).OrderBy(c => c.StartAt))
        {
            var line = client.Account.Name;
            var reference = startOfRun;
            (line, reference) = AddOffset(reference, client.StartAt, line!);
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
                launcher.ClientReady += LauncherClientReady;
                launcher.ClientClosed += LauncherClientClosed;
                _ = await launcher.LaunchAsync(settingsController.DatFile!, applicationFolder, settingsController.GfxSettingsFile!, authenticationThrottle, loginSemaphore, exeSemaphore);
                LogManager.Flush();
            }, cancellationToken))
            .ToList();
        Logger.Debug("{0} launch tasks primed.", tasks.Count);
        return tasks;
    }

    private List<Client> readyClients { get; }
    private Client? activeClient;
    private void LauncherClientReady(object? sender, EventArgs e)
    {
        if (sender is not Client client) return;
        if (!readyClients.Contains(client)) readyClients.Add(client);
        if (activeClient != null) return;
        activeClient = client;
        activeClient.RestoreWindow();
    }

    private void LauncherClientClosed(object? sender, EventArgs e)
    {
        if (sender is not Client client) return;
        if (readyClients.Contains(client)) readyClients.Remove(client);
        if (activeClient != client) return;
        var next = readyClients.FirstOrDefault();
        if (next == null)
        {
            activeClient = null;
            return;
        }
        activeClient = next;
        activeClient.RestoreWindow();
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