namespace guildwars2.tools.alternator;

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


    public ClientController(DirectoryInfo applicationFolder,
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

        logFactory = new LogFactory();

        ReadyClients = new List<Client>();
        loginSemaphore = new SemaphoreSlim(0, 1);
    }

    private record VpnAccounts(VpnDetails Vpn, List<IAccount> Accounts);

    public ILogger? LaunchLogger { get; private set; }
    private readonly LogFactory? logFactory;
    public void ClearLogging()
    {
        logFactory?.Shutdown();
    }

    public async Task LaunchMultiple(
        List<IAccount> selectedAccounts,
        IAccountCollection accountCollection,
        bool all,
        bool shareArchive,
        bool accountLogs,
        bool ignoreVpn,
        int maxInstances,
        int vpnAccountSize,
        CancellationTokenSource cancellationTokenSource
        )
    {
        ReadyClients.Clear();
        vpnCollection.ResetConnections();

        var accounts = selectedAccounts.Any() ? selectedAccounts : accountCollection.AccountsToRun(launchType, all);

        if (accounts == null || !accounts.Any())
        {
            Logger.Debug("No accounts to run.");
            return;
        }

        if (logFactory != null)
        {
            var fileTarget = new FileTarget("AccountLogger")
            {
                FileName = Path.Combine(applicationFolder.FullName, $"gw2-alternator-launch-{launchType}-log.txt"),
                Layout = new SimpleLayout {Text = "${longdate}|${message:withexception=true}"},
                ArchiveOldFileOnStartup = true,
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                MaxArchiveDays = 14,
            };
            var config = new LoggingConfiguration();
            config.AddTarget(fileTarget);
            config.AddRuleForAllLevels(fileTarget);
            logFactory.Configuration = config;

            LaunchLogger = logFactory.GetCurrentClassLogger();
            Logger.Debug("Launch Logging to {0}", fileTarget.FileName);
            Logger.Info($"GW2-Alternator Version: {MainViewModel.Version}");
            Logger.Info($"GW2 Client Version: {MainViewModel.Gw2ClientBuild}");
            LaunchLogger.Info($"GW2-Alternator Version: {MainViewModel.Version}");
            LaunchLogger.Info($"GW2 Client Version: {MainViewModel.Gw2ClientBuild}");
        }

        foreach (var account in accounts)
        {
            account.Reset();
        }

        var vpnsUsed = new List<VpnDetails>();
        var clients = new List<Client>();
        var first = true;
        var start = DateTime.UtcNow;
        try
        {
            RenameAddonsFolder();
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            Logger.Debug("Max GW2 Instances={0}", maxInstances);

            var accountsByVpn = AccountCollection.AccountsByVpn(accounts, ignoreVpn);
            while (accounts.Any(a => !a.Done))
            {
                LaunchLogger?.Info("{0} accounts left", accounts.Count(a => !a.Done));

                var now = DateTime.UtcNow;

                var accountsByVpnDetails = accountsByVpn
                    .Select(kv => new VpnAccounts(vpnCollection.GetVpn(kv.Key), kv.Value.Where(a => !a.Done).ToList()))
                    .Where(d => d.Accounts.Any())
                    .ToList();

                var vpnSets = accountsByVpnDetails
                    .OrderBy(s => s.Vpn.Available(now.Subtract(new TimeSpan(1,0,0)), launchType==LaunchType.Update))
                    .ThenBy(s => s.Vpn.RecentFailures)
                    .ThenBy(s => s.Vpn.GetPriority(s.Accounts.Count, vpnAccountSize))
                    .ToList();

                Logger.Debug("{0} launch sets found", vpnSets.Count);

                var (vpn, vpnAccounts) = vpnSets.First();

                var accountsAvailableToLaunch = vpnAccounts
                    .OrderBy(a => a.Available(now)).ToList();

                var accountsToLaunch = accountsAvailableToLaunch
                    .OrderBy(a => a.VpnPriority)
                    .Take(vpnAccountSize)
                    .ToList();

                Logger.Debug("{0} VPN Chosen with {1} accounts", vpn.DisplayId, accountsToLaunch.Count);
                LaunchLogger?.Info("{0} VPN Chosen with {1} accounts", vpn.DisplayId, accountsToLaunch.Count);

                if (!accountsToLaunch.Any()) continue;

                if (launchType == LaunchType.Login) KillAllGw2();

                var clientsToLaunch = new List<Client>();
                foreach (var account in accountsToLaunch)
                {
                    Logger.Debug("Launching client for Account {0}", account.Name);
                    clientsToLaunch.Add(await account.NewClient(LaunchLogger));
                }
                clients.AddRange(clientsToLaunch);

                var waitUntil = vpn.Available(now, launchType == LaunchType.Update).Subtract(now);
                if (waitUntil.TotalSeconds > 0)
                {
                    Logger.Debug("VPN {0} on login cooldown {1}", vpn.Id, waitUntil);
                    await Task.Delay(waitUntil, cancellationTokenSource.Token);
                }

                try
                {
                    vpn.Cancellation = new CancellationTokenSource();
                    var vpnToken = vpn.Cancellation.Token;
                    vpnToken.ThrowIfCancellationRequested();
                    var doubleTrouble = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, vpnToken);
                    if (!vpnsUsed.Contains(vpn)) vpnsUsed.Add(vpn);
                    var status = await vpn.Connect(cancellationTokenSource.Token);
                    if (status != null)
                    {
                        Logger.Error("VPN {0} Connection {1} : {2}", vpn.Id, vpn.ConnectionName, status);
                        LaunchLogger?.Info("VPN {0} Connection {1} : {2}", vpn.Id, vpn.ConnectionName, status);
                        continue;
                    }

                    Logger.Debug("Launching {0} clients", clientsToLaunch.Count);
                    LaunchLogger?.Info("Launching {0} clients", clientsToLaunch.Count);
                    var tasks = PrimeLaunchTasks(vpn, clientsToLaunch, shareArchive, accountLogs, exeSemaphore, doubleTrouble.Token);
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
                catch (OperationCanceledException ce)
                {
                    authenticationThrottle.Reset();
                    Logger.Debug("Cancelled {0} clients because {1}", clientsToLaunch.Count(c => !c.Account.Done), ce.CancellationToken.CancellationReason());
                    return;
                }
                catch (Exception e)
                {
                    authenticationThrottle.Reset();
                    Logger.Error(e, "VPN {0} failure detected, skipping", vpn.Id);
                }
                finally
                {
                    var status = await vpn.Disconnect(cancellationTokenSource.Token);
                    if (status != null)
                    {
                        Logger.Error("VPN {0} Disconnection failed : {1}", vpn, status);
                    }
                    else
                    {
                        authenticationThrottle.CurrentVpn = null;
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
            cancellationTokenSource.Cancel(true, "Finalisation");
            await Restore(first);
            Logger.Info("GW2 account files restored.");
            await SaveMetrics(start, clients, vpnsUsed);
        }
    }

    private void KillAllGw2()
    {
        Parallel.ForEach(Process.GetProcessesByName("Gw2-64"), process =>
        {
            Logger.Info($"Killing GW2 rogue process {process.Id}");
            process.Kill();
        });
    }

    private const string AddonsFolderName = "addons";

    private void RenameAddonsFolder()
    {
        try
        {
            RenameSubFolder(settingsController.Settings!.Gw2Folder, AddonsFolderName, $"{AddonsFolderName}-temp");
            Logger.Info("Moving addons folder temporarily, for efficiency");
        }
        catch (Exception e)
        {
            Logger.Error(e, "LaunchMultiple: unexpected error renaming addons folder");
            RestoreAddonsFolder();
        }
    }

    private void RestoreAddonsFolder()
    {
        try
        {
            RenameSubFolder(settingsController.Settings!.Gw2Folder, $"{AddonsFolderName}-temp", AddonsFolderName);
            Logger.Info("Restored addons folder");
        }
        catch (Exception e)
        {
            Logger.Error(e, "LaunchMultiple: unexpected error restoring addons folder");
        }
    }

    private void RenameSubFolder(string? parent, string from, string to)
    {
        if (!Directory.Exists(parent)) return;

        var addonsBackupFolderPath = new DirectoryInfo(Path.Combine(parent, from));
        if (!addonsBackupFolderPath.Exists) return;

        var addonsFolderPath = Path.Combine(parent, to);
        if (Directory.Exists(addonsFolderPath)) return;

        addonsBackupFolderPath.MoveTo(addonsFolderPath);
    }

    private async Task SaveMetrics(DateTime startOfRun, List<Client> clients, List<VpnDetails> vpnDetailsList)
    {
        Logger.Debug("Metrics being saved");

        (string, DateTime) AddOffset(DateTime reference, DateTime time, string line)
        {
            if (time < reference) return (line + "\t", reference);
            line += $"\t{time.Subtract(reference).TotalSeconds.ToString(CultureInfo.InvariantCulture)}";
            return (line, time);
        }

        var validClients = clients.Where(c => c.Account.Name != null && c.StartAt > DateTime.MinValue).ToList();
        var lines = new List<string>
        {
            $"Started\t{startOfRun:d}\t{startOfRun:T}\t?\t{MainViewModel.Version}",
            $"Total Time\t{DateTime.UtcNow.Subtract(startOfRun).TotalSeconds}\ts",
            $"Attempts\\Fails\t{validClients.Count}\t{validClients.Count(c => c.ExitReason != ExitReason.Success)}\t{(double)validClients.Count / validClients.Select(c => c.Account).Distinct().Count():0.###}",
            "Account\tStart\tAuthenticate\tLogin\tEnter\tExit",
        };

        foreach (var client in validClients.OrderBy(c => c.StartAt))
        {
            //Logger.Debug("Client {0} {1} {2}", client.Account.Name, client.AccountIndex, client.StartAt);
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
        Logger.Info("Metrics saved to {0}", metricsFileUniquePath);
        MetricsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private List<Task> PrimeLaunchTasks(
        VpnDetails vpnDetails, 
        IEnumerable<Client> clients,
        bool shareArchive,
        bool accountLogs,
        SemaphoreSlim exeSemaphore,
        CancellationToken cancellationToken)
    {
        var tasks = clients.Select(client => Task.Run(async () =>
            {
                var launcher = new Launcher(client, launchType, applicationFolder, settingsController.Settings!, vpnDetails, cancellationToken);
                launcher.ClientReady += LauncherClientReady;
                launcher.ClientClosed += LauncherClientClosed;
                _ = await launcher.LaunchAsync(
                    settingsController.DatFile!, 
                    applicationFolder, 
                    settingsController.GfxSettingsFile!, 
                    shareArchive,
                    accountLogs,
                    authenticationThrottle, 
                    loginSemaphore, 
                    exeSemaphore
                    );
                LogManager.Flush();
            }, cancellationToken))
            .ToList();
        Logger.Debug("{0} launch tasks primed.", tasks.Count);
        return tasks;
    }

    private List<Client> ReadyClients { get; }
    private Client? activeClient;

    private void LauncherClientReady(object? sender, EventArgs e)
    {
        if (sender is not Client client) return;
        if (!ReadyClients.Contains(client)) ReadyClients.Add(client);
        if (activeClient != null) return;
        activeClient = client;
        activeClient.RestoreWindow();
    }

    private void LauncherClientClosed(object? sender, EventArgs e)
    {
        if (sender is not Client client) return;
        if (ReadyClients.Contains(client)) ReadyClients.Remove(client);
        if (activeClient != client) return;
        var next = ReadyClients.FirstOrDefault();
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
            RestoreAddonsFolder();
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