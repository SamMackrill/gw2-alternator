namespace guildwars2.tools.alternator;

public class AuthenticationThrottle : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Settings settings;
    private readonly Counter launchCount;
    private readonly Counter failedCount;
    private readonly SemaphoreSlim authenticationSemaphore;

    private readonly ConcurrentDictionary<string, Client> failedClients;

    public AuthenticationThrottle(Settings? settings)
    {
        this.settings = settings ?? throw new NullReferenceException(nameof(settings));
        authenticationSemaphore = new SemaphoreSlim(1, 1);
        launchCount = new Counter();
        failedCount = new Counter();
        failedClients = new ConcurrentDictionary<string, Client>();
    }

    public async Task WaitAsync(Client client, VpnDetails vpnDetails, CancellationToken launchCancelled)
    {
        await authenticationSemaphore.WaitAsync(launchCancelled);
        Logger.Info("{0} Authentication Semaphore Entered", client.Account.Name);
        launchCount.Increment();
        CurrentVpn = vpnDetails;
        vpnDetails.SetAttempt(settings);
        OnPropertyChanged(nameof(Vpn));
    }

    private DateTime FreeAt { get; set; }
    public double FreeIn => FreeAt.Subtract(DateTime.Now).TotalSeconds;

    public string? Vpn => CurrentVpn?.Id;

    private Task? releaseTask;
    private VpnDetails? currentVpn;

    public VpnDetails? CurrentVpn
    {
        get => currentVpn;
        set
        {
            if (SetProperty(ref currentVpn, value))
            {
                OnPropertyChanged(nameof(Vpn));
            }
        }
    }

    public void Reset()
    {
        Logger.Info("Authentication Semaphore Released");
        authenticationSemaphore.Release();
    }


    Client? releaseTaskClient;

    public async Task LoginDone(VpnDetails vpnDetails, Client client, LaunchType launchType, CancellationToken launchCancelled)
    {
        Logger.Debug("{0} LoginDone {1}", client.Account.Name, launchType);
        if (releaseTask is {IsCompleted: false})
        {
            Logger.Debug("{0} Warning previous release task still active!", releaseTaskClient?.Account.Name ?? "Unknown");
        }
        releaseTaskClient = client;

        releaseTask = Task.Factory.StartNew(async () =>
        {
            await Release(vpnDetails, client, launchType, launchCancelled);
        });
    }

    private async Task Release(VpnDetails vpnDetails, Client client, LaunchType launchType, CancellationToken launchCancelled)
    {
        var account = client.Account;
        try
        {
            if (launchType is LaunchType.Update) return;

            Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
            var delay = vpnDetails.Delay;
            Logger.Debug("{0} Authentication {1} VPN release delay={2}s", account.Name, vpnDetails.DisplayId, delay);
            if (delay > 0)
            {
                FreeAt = DateTime.Now.AddSeconds(delay);
                await Task.Delay(new TimeSpan(0, 0, delay), launchCancelled);
            }
            else
            {
                FreeAt = DateTime.MinValue;
            }
        }
        finally
        {
            Logger.Info("{0} Authentication Semaphore Released", account.Name);
            authenticationSemaphore.Release();
        }
    }

    public void LoginFailed(VpnDetails vpnDetails, Client client)
    {
        failedCount.Increment();
        if (client.Account.Name != null) failedClients.AddOrUpdate(client.Account.Name, client, (_, _) => client);
        vpnDetails.SetFail(client.Account);
    }

    public void LoginSucceeded(VpnDetails vpnDetails, Client client)
    {
        if (client.Account.Name != null) failedClients.Remove(client.Account.Name, out _);
        vpnDetails.SetSuccess();
    }

}