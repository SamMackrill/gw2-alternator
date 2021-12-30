using System.Text.Json;
using System.Xml;
using System.Xml.XPath;

namespace guildwars2.tools.alternator.MVVM.model;

public class AccountCollection
{
    private readonly string launchbuddyFolder;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string AccountsJsonFile = "accounts.json";

    public List<Account>? Accounts { get; private set; }

    private readonly string accountsJson;
    private readonly SemaphoreSlim semaphore;

    public event EventHandler<EventArgs>? Loaded;

    public AccountCollection(FileSystemInfo folderPath, string launchbuddyFolder)
    {
        this.launchbuddyFolder = launchbuddyFolder;
        accountsJson = Path.Combine(folderPath.FullName, AccountsJsonFile);
        semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task Save()
    {
        try
        {
            Logger.Debug("Saving Accounts to {0}", accountsJson);
            await semaphore.WaitAsync();

            await using (var stream = new FileStream(accountsJson, FileMode.Create))
            {
                await JsonSerializer.SerializeAsync(stream, Accounts, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            }

            await Task.Delay(1000);
            Logger.Debug("Accounts saved to {0}", accountsJson);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task Load()
    {
        try
        {
            await semaphore.WaitAsync();
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
            semaphore.Release();
        }
    }

    public List<Account>? AccountsToRun(LaunchType launchType, bool all)
    {
        if (Accounts == null) return null;

        return launchType switch
        {
            LaunchType.Login => Accounts.Where(a => all || a.LoginRequired).ToList(),
            LaunchType.Collect => Accounts.Where(a => all || a.CollectionRequired).OrderBy(a =>a.LastCollection).ToList(),
            LaunchType.Update => Accounts.Where(a => all || a.UpdateRequired).ToList(),
            _ => throw new ArgumentException(message: "invalid enum value", paramName: nameof(launchType))
        };
    }

    public bool CanImportFromLaunchbuddy => Directory.Exists(launchbuddyFolder);

    public void ImportFromLaunchbuddy()
    {
        try
        {
            semaphore.Wait();
            var accountsXml = Path.Combine(launchbuddyFolder, "Accs.xml");
            var doc = new XPathDocument(accountsXml);
            var navigator = doc.CreateNavigator();
            //var LBAccounts = navigator.SelectChildren();
            Logger.Debug("{0} Accounts loaded from {1}", 9, accountsXml);
            Loaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading accounts from {accountsJson}");
        }
        finally
        {
            semaphore.Release();
        }
    }
}