namespace guildwars2.tools.alternator;

public class AuthenticationThrottle
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Settings settings;
    private readonly Counter launchCount;
    private readonly Counter failedCount;
    private Counter consecutiveFailedCount;
    private readonly SemaphoreSlim authenticationSemaphore;

    private Client? liveClient;
    private readonly ConcurrentDictionary<string, Client> failedClients;

    public AuthenticationThrottle(Settings settings)
    {
        this.settings = settings;
        authenticationSemaphore = new SemaphoreSlim(1, 1);
        launchCount = new Counter();
        failedCount = new Counter();
        consecutiveFailedCount = new Counter();
        failedClients = new ConcurrentDictionary<string, Client>();
    }

    public async Task WaitAsync(Client client, CancellationToken launchCancelled)
    {
        await authenticationSemaphore.WaitAsync(launchCancelled);
        liveClient = client;
        launchCount.Increment();
    }

    public DateTime FreeAt { get; private set; }
    public double FreeIn => FreeAt.Subtract(DateTime.Now).TotalSeconds;

    private Task? releaseTask;
    public async Task LoginDone(Client client, LaunchType launchType, CancellationToken launchCancelled)
    {
        if (releaseTask != null) await releaseTask.WaitAsync(launchCancelled);

        releaseTask = Task.Factory.StartNew(async () =>
        {
            var account = client.Account;
            try
            {
                if (launchType is LaunchType.Update) return;

                Logger.Debug("{0} launchCount={1}", account.Name, launchCount.Count);
                var delay = LaunchDelay(launchCount.Count, client.Attempt, client.FailedCount);
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

    public void LoginFailed(Client client, LaunchType launchType, CancellationToken launchCancelled)
    {
        failedCount.Increment();
        consecutiveFailedCount.Increment();
        failedClients.AddOrUpdate(client.Account.Name, client, (_, _) => client);
    }

    public void LoginSucceeded(Client client, LaunchType launchType, CancellationToken launchCancelled)
    {
        consecutiveFailedCount = new Counter();
        failedClients.Remove(client.Account.Name, out _);
    }

    private int LaunchDelay(int count, int attempt, int clientFailedCount)
    {
        if (attempt > 1) return 60 + 30 * (1 << (attempt - 1));

        var delay = BandDelay(count);

        if (failedCount.Count > 0) delay = Math.Max(delay, 60);
        if (clientFailedCount > 2) delay = Math.Max(delay, 60 * (clientFailedCount - 2));
        if (consecutiveFailedCount.Count > 0) delay = Math.Max(delay, 60 * consecutiveFailedCount.Count);

        return delay;
    }

    private int BandDelay(int count)
    {
        if (count < settings.AccountBand1) return settings.AccountBand1Delay;
        if (count < settings.AccountBand2) return settings.AccountBand2Delay;
        if (count < settings.AccountBand3) return settings.AccountBand3Delay;
        return settings.AccountBand3Delay + 60;
    }
}