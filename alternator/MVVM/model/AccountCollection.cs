using System.Text.Json;
using System.Xml.Linq;

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
            if (!File.Exists(accountsJson)) return;
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
            var accountsXmlPath = Path.Combine(launchbuddyFolder, "Accs.xml");
            var doc = XDocument.Load(accountsXmlPath);
            if (doc.Root == null) return;
            var LBAccounts = doc.Root.Elements().ToArray();
            Logger.Debug("{0} Accounts loaded from {1}", LBAccounts.Length, accountsXmlPath);
            if (!LBAccounts.Any()) return;

            Accounts ??= new List<Account>();
            foreach (var lbAccount in LBAccounts)
            {
                var nickname = lbAccount.Element("Nickname")?.Value;
                if (nickname == null) continue;
                var nameParts = nickname.Split('-', 3, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                string? accountName = null;
                string? characterName = null;
                switch (nameParts.Length)
                {
                    case 1:
                        accountName = nameParts[0];
                        break;
                    case 2:
                        accountName = nameParts[0];
                        characterName = nameParts[1];
                        break;
                    case 3:
                        accountName = nameParts[1];
                        characterName = nameParts[2];
                        break;
                }
                if (accountName == null) continue;

                var settings = lbAccount.Element("Settings");
                var pathToLoginDat = settings?.Element("Loginfile")?.Element("Path")?.Value;
                if (pathToLoginDat == null || !File.Exists(pathToLoginDat)) continue;
                var lastLoginText = settings?.Element("AccountInformation")?.Element("LastLogin")?.Value;
                var account = Accounts.FirstOrDefault(a => string.Equals(a.Name, accountName, StringComparison.OrdinalIgnoreCase));
                if (account != null)
                {
                    account.LoginFilePath = pathToLoginDat;
                    account.Character ??= characterName;
                }
                else
                {
                    account = new Account(accountName, characterName, pathToLoginDat);
                    Accounts.Add(account);
                }

                if (DateTime.TryParse(lastLoginText, out var lastLogin))
                {
                    if (lastLogin>account.LastLogin) account.LastLogin = lastLogin;
                }

            }
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