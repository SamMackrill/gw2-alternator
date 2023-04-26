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
    public double FreeIn => FreeAt.Subtract(DateTime.UtcNow).TotalSeconds;

    public string Reason { get; private set; }

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

#pragma warning disable CS1998
    public async Task LoginDone(VpnDetails vpnDetails, Client client, LaunchType launchType, CancellationToken launchCancelled)
#pragma warning restore CS1998
    {
        Logger.Debug("{0} LoginDone {1}", client.Account.Name, launchType);
        client.LaunchLogger?.Debug("{0} LoginDone {1}", client.Account.Name, launchType);
        if (releaseTask is {IsCompleted: false})
        {
            Logger.Debug("{0} Warning previous release task still active!", releaseTaskClient?.Account.Name ?? "Unknown");
        }
        releaseTaskClient = client;

        // ReSharper disable once MethodSupportsCancellation
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
            var (delay, why) = vpnDetails.LaunchDelay(false);
            Logger.Debug("{0} Authentication {1} release delay={2}s because {3}", account.Name, vpnDetails.DisplayId, delay, why);
            client.LaunchLogger?.Debug("{0} Authentication {1} release delay={2}s because {3}", account.Name, vpnDetails.DisplayId, delay, why);
            if (delay > 0)
            {
                Reason = why;
                client.LaunchLogger?.Debug("{0} Authentication Throttle set: {1}s because {2}", account.Name, delay, why);
                client.AccountLogger?.Debug("Authentication Throttle set: {1}s because {2}", account.Name, delay, why);
                FreeAt = DateTime.UtcNow.AddSeconds(delay);
                await Task.Delay(new TimeSpan(0, 0, delay), launchCancelled);
                client.LaunchLogger?.Debug("{0} Authentication Throttle Released", account.Name);
                client.AccountLogger?.Debug("Authentication Throttle Released", account.Name);
            }
            else
            {
                FreeAt = DateTime.MinValue;
            }
        }
        finally
        {
            Logger.Info("{0} Authentication Semaphore Released", account.Name);
            client.AccountLogger?.Debug("Authentication Semaphore Released", account.Name);
            authenticationSemaphore.Release();
        }
    }

    public void LoginFailed(VpnDetails vpnDetails, Client client, bool cancelVpn)
    {
        failedCount.Increment();
        if (client.Account.Name != null) failedClients.AddOrUpdate(client.Account.Name, client, (_, _) => client);
        vpnDetails.SetFail(client.Account, cancelVpn);
    }

    public void LoginSucceeded(VpnDetails vpnDetails, Client client)
    {
        if (client.Account.Name != null) failedClients.Remove(client.Account.Name, out _);
        vpnDetails.SetSuccess();
    }

}