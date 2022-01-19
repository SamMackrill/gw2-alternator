﻿namespace guildwars2.tools.alternator;

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
    private readonly VpnCollection vpnCollection;

    public event EventHandler<GenericEventArgs<bool>>? AfterLaunchClient;

    public ClientController(DirectoryInfo applicationFolder, SettingsController settingsController,
        AuthenticationThrottle authenticationThrottle, VpnCollection vpnCollection, LaunchType launchType)
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

    public async Task LaunchMultiple(List<IAccount> selectedAccounts, AccountCollection accountCollection, bool all,
        bool ignoreVpn, int maxInstances, CancellationTokenSource cancellationTokenSource)
    {

        var accounts = selectedAccounts.Any() ? selectedAccounts : accountCollection.AccountsToRun(launchType, all);

        if (accounts == null || !accounts.Any())
        {
            Logger.Debug("No accounts to run.");
            return;
        }

        try
        {
            var exeSemaphore = new SemaphoreSlim(0, maxInstances);
            Logger.Debug("Max GW2 Instances={0}", maxInstances);

            if (ignoreVpn)
            {
                var tasks = PrimeLaunchTasks(accounts.Select(a => a.Client)!, exeSemaphore, cancellationTokenSource.Token);
                await Task.Delay(200, cancellationTokenSource.Token);
                if (cancellationTokenSource.IsCancellationRequested) return;

                // Release the hounds
                exeSemaphore.Release(maxInstances);
                loginSemaphore.Release(1);

                await Task.WhenAll(tasks.ToArray());
            }
            else
            {
                var first = true;
                var clientsByVpn = AccountCollection.ClientsByVpn(accounts);
                while (clientsByVpn.SelectMany(c => c.Value).Distinct().Any(c => c.RunStatus != RunState.Completed))
                {
                    var incompleteClients = clientsByVpn.Select(v => new
                    { Id = v.Key, Clients = v.Value.Where(c => c.RunStatus != RunState.Completed).ToList() });
                    foreach (var vpnSet in incompleteClients.OrderByDescending(a => a.Clients.Count))
                    {
                        var vpn = vpnCollection.VPN?.FirstOrDefault(v => v.Id == vpnSet.Id);
                        if (vpn == null && !string.IsNullOrEmpty(vpnSet.Id))
                        {
                            Logger.Debug($"Unknown VPN {vpnSet.Id}, skipping");
                            continue;
                        }

                        try
                        {
                            if (vpn != null)
                            {
                                Logger.Info($"Connecting to VPN {vpn}");
                                var vpnProcess = Process.Start("rasdial", $@"""{vpn.ConnectionName}""");
                                await vpnProcess.WaitForExitAsync(cancellationTokenSource.Token);
                            }

                            var tasks = PrimeLaunchTasks(vpnSet.Clients.Take(10), exeSemaphore, cancellationTokenSource.Token);
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
                            if (vpn != null)
                            {
                                Logger.Info($"Disconnecting from VPN {vpn}");
                                var vpnProcess = Process.Start("rasdial", $@"""{vpn.ConnectionName}"" /d");
                                await vpnProcess.WaitForExitAsync(cancellationTokenSource.Token);
                            }
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
            await Restore();
            Logger.Info("GW2 account files restored.");
        }
    }

    private List<Task> PrimeLaunchTasks(IEnumerable<Client> clients, SemaphoreSlim exeSemaphore, CancellationToken cancellationToken)
    {
        var tasks = clients.Select(client => Task.Run(async () =>
            {
                var launcher = new Launcher(client, launchType, applicationFolder, settings,
                    cancellationToken);
                var success = await launcher.LaunchAsync(loginFile, applicationFolder, gfxSettingsFile, authenticationThrottle, loginSemaphore, exeSemaphore);
                AfterLaunchClient?.Invoke(client, new GenericEventArgs<bool>(success));
                LogManager.Flush();
            }, cancellationToken))
            .ToList();
        Logger.Debug("{0} launch tasks primed.", tasks.Count);
        return tasks;
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