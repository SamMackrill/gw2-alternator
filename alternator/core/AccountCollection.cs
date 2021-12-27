using System.Text.Json;

namespace guildwars2.tools.alternator;

public class AccountCollection
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public List<Account>? Accounts { get; private set; }
    private readonly string accountsJson;
    private readonly SemaphoreSlim accountsSemaphore;

    public event EventHandler<EventArgs>? Loaded;

    public AccountCollection(string accountsJson)
    {
        this.accountsJson = accountsJson;
        accountsSemaphore = new SemaphoreSlim(1, 1);
    }

    public async Task Save()
    {
        try
        {
            Logger.Debug("Saving Accounts to {0}", accountsJson);
            await accountsSemaphore.WaitAsync();

            await using (var stream = new FileStream(accountsJson, FileMode.Create))
            {
                await JsonSerializer.SerializeAsync(stream, Accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            }

            await Task.Delay(1000);
            Logger.Debug("Accounts saved to {0}", accountsJson);
        }
        finally
        {
            accountsSemaphore.Release();
        }
    }

    public async Task Load()
    {
        try
        {
            await accountsSemaphore.WaitAsync();
            await using var stream = File.OpenRead(accountsJson);
            Accounts = await JsonSerializer.DeserializeAsync<List<Account>>(stream);
            Logger.Debug("Accounts loaded from {0}", accountsJson);
            Loaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading accounts from {accountsJson}");
        }
        finally
        {
            accountsSemaphore.Release();
        }
    }

    public List<Account>? AccountsToRun(LaunchType launchType, bool all)
    {
        if (Accounts == null) return null;
        if (all) return Accounts;

        return launchType switch
        {
            LaunchType.Login => Accounts.Where(a => a.LoginRequired).ToList(),
            LaunchType.Collect => Accounts.Where(a => a.CollectionRequired).ToList(),
            LaunchType.Update => Accounts.Where(a => a.UpdateRequired).ToList(),
            _ => throw new ArgumentException(message: "invalid enum value", paramName: nameof(launchType))
        };
    }
}