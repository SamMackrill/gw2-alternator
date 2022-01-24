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

    public async Task WaitAsync(VpnDetails vpnDetails, CancellationToken launchCancelled)
    {
        await authenticationSemaphore.WaitAsync(launchCancelled);
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

    public async Task LoginDone(VpnDetails vpnDetails, Client client, LaunchType launchType, CancellationToken launchCancelled)
    {
        if (releaseTask != null) await releaseTask.WaitAsync(launchCancelled);

        releaseTask = Task.Factory.StartNew(async () =>
        {
            var account = client.Account;
            try
            {
                if (launchType is LaunchType.Update) return;

                Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
                var delay = vpnDetails.Delay;
                Logger.Debug("{0} Authentication Semaphore delay={1}s", account.Name, delay);
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
                Logger.Debug("{0} Authentication Semaphore Released", account.Name);
                authenticationSemaphore.Release();
            }
        }, launchCancelled);
    }

    public void LoginFailed(VpnDetails vpnDetails, Client client)
    {
        failedCount.Increment();
        failedClients.AddOrUpdate(client.Account.Name, client, (_, _) => client);
        vpnDetails.SetFail();
    }

    public void LoginSucceeded(VpnDetails vpnDetails, Client client)
    {
        failedClients.Remove(client.Account.Name, out _);
        vpnDetails.SetSuccess();
    }

}