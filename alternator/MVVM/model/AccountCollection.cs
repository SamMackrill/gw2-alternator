using System.Text.Json;
using System.Xml.Linq;

namespace guildwars2.tools.alternator.MVVM.model;

public interface IAccountCollection : IJsonCollection
{
    List<IAccount>? Accounts { get; }
    bool CanImportFromLaunchbuddy { get; }
    bool CanImportFromLauncher { get; }
    List<IAccount>? AccountsToRun(LaunchType launchType, bool all);
    void ImportFromLaunchbuddy();
    void ImportFromLauncher();
    void SetUndo();
    bool Any();
}

public class AccountCollection : JsonCollection<Account>, IAccountCollection
{
    private readonly string launchbuddyFolder;
    private readonly string launcherFolder;

    private const string AccountsJsonFile = "accounts.json";

    public List<IAccount>? Accounts => Items?.Cast<IAccount>().ToList();

    public override event EventHandler? Loaded;
    public override event EventHandler? LoadFailed;
    public override event EventHandler? Updated;


    public AccountCollection(FileSystemInfo folderPath, string launchbuddyFolder, string launcherFolder) : base(folderPath, AccountsJsonFile)
    {
        this.launchbuddyFolder = launchbuddyFolder;
        this.launcherFolder = launcherFolder;
    }

    public override async Task Save()
    {
        try
        {
            Logger.Debug("Saving Accounts to {0}", vpnJson);
            await semaphore.WaitAsync();

            await using (var stream = new FileStream(vpnJson, FileMode.Create))
            {
                await JsonSerializer.SerializeAsync(stream, Items, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            }

            Logger.Debug("Accounts saved to {0}", vpnJson);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public override async Task Load()
    {
        try
        {
            await semaphore.WaitAsync();
            if (!File.Exists(vpnJson)) throw new FileNotFoundException();
            await using var stream = File.OpenRead(vpnJson);
            Items = await JsonSerializer.DeserializeAsync<List<Account>>(stream);
            Logger.Debug("Accounts loaded from {0}", vpnJson);
            Loaded?.Invoke(this, EventArgs.Empty);
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading accounts from {vpnJson}");
            LoadFailed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public List<IAccount>? AccountsToRun(LaunchType launchType, bool all)
    {
        if (Accounts == null) return null;

        return launchType switch
        {
            LaunchType.Login => Accounts.Where(a => all || a.LoginRequired).ToList(),
            LaunchType.Collect => Accounts.Where(a => all || a.CollectionRequired).OrderBy(a => a.LastCollection).ToList(),
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
            var accountsXmlPath = Path.Combine(launchbuddyFolder, @"Accs.xml");
            var doc = XDocument.Load(accountsXmlPath);
            if (doc.Root == null) return;
            var lbAccounts = doc.Root.Elements().ToArray();
            Logger.Debug("{0} Accounts loaded from {1}", lbAccounts.Length, accountsXmlPath);
            if (!lbAccounts.Any()) return;

            Items ??= new List<Account>();
            foreach (var lbAccount in lbAccounts)
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
                var account = Accounts!.FirstOrDefault(a => string.Equals(a.Name, accountName, StringComparison.OrdinalIgnoreCase));
                if (account != null)
                {
                    account.LoginFilePath = pathToLoginDat;
                    account.Character ??= characterName;
                }
                else
                {
                    var newAccount = new Account(accountName, characterName, pathToLoginDat);
                    Items.Add(newAccount);
                    account = newAccount;
                }

                if (DateTime.TryParse(lastLoginText, out var lastLogin))
                {
                    if (lastLogin > account.LastLogin) account.LastLogin = lastLogin;
                }

            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading accounts from {launchbuddyFolder}");
        }
        finally
        {
            semaphore.Release();
            if (Any())
            {
                Loaded?.Invoke(this, EventArgs.Empty);
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool CanImportFromLauncher => Directory.Exists(launchbuddyFolder);

    public void ImportFromLauncher()
    {

        bool[] ExpandBooleans(byte[] bytes)
        {
            var bools = new bool[bytes.Length * 8];

            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                if (b <= 0) continue;

                var p = i * 8;
                for (var j = 0; j < 8; j++)
                {
                    bools[p + j] = (b >> 7 - j & 1) == 1;
                }
            }
            return bools;
        }

        int ReadVariableLength(BinaryReader reader)
        {
            int b = reader.ReadByte();
            if (b != byte.MaxValue) return b;

            var s = reader.ReadUInt16();
            if (s != ushort.MaxValue) return s;

            return reader.ReadInt32();
        }

        Items ??= new List<Account>();
        try
        {
            semaphore.Wait();

            // This thing is a total nightmare! Why would you cause so much pain?

            var expectedHeader = new byte[] { 41, 229, 122, 91, 23 };
            var SortingOptions_ARRAY_SIZE = 2;

            var settingsPath = Path.Combine(launcherFolder, "settings.dat");

            using (var reader = new BinaryReader(new BufferedStream(File.Open(settingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))))
            {
                var header = reader.ReadBytes(expectedHeader.Length);
                if (!header.IsTheSameAs(expectedHeader)) throw new IOException("Invalid GW2-Launcher settings header");
                var version = reader.ReadUInt16();

                if (version != 11) throw new IOException("Only GW2-Launcher V11 supported");

                var count = reader.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadByte();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt32();
                }

                var booleans = ExpandBooleans(reader.ReadBytes(reader.ReadByte()));

                if (booleans[0]) _ = reader.ReadBytes(SortingOptions_ARRAY_SIZE);

                if (booleans[1])
                {
                    _ = reader.ReadByte();
                    _ = reader.ReadBytes(reader.ReadByte());
                }

                if (booleans[7]) _ = reader.ReadString();
                if (booleans[8]) _ = reader.ReadString();
                if (booleans[9]) _ = reader.ReadInt32();
                if (booleans[10]) _ = reader.ReadString();
                if (booleans[11]) _ = reader.ReadString();

                if (booleans[20])
                {
                    _ = reader.ReadInt64();
                    _ = reader.ReadUInt16();
                }

                if (booleans[21]) _ = reader.ReadUInt16();
                if (booleans[24]) _ = reader.ReadByte();

                if (booleans[25])
                {
                    _ = reader.ReadByte();
                    _ = reader.ReadByte();
                }

                if (booleans[26]) _ = reader.ReadByte();
                if (booleans[27]) _ = reader.ReadInt32();
                if (booleans[28]) _ = reader.ReadByte();

                if (booleans[29])
                {
                    var count29 = ReadVariableLength(reader);
                    for (var i = 0; i < count29; i++)
                    {
                        _ = reader.ReadString();
                        _ = reader.ReadString();
                        _ = reader.ReadString();
                        _ = reader.ReadByte();
                    }
                }

                if (booleans[30]) _ = reader.ReadByte();
                if (booleans[36]) _ = reader.ReadUInt16();
                if (booleans[37]) _ = reader.ReadString();
                if (booleans[41]) _ = reader.ReadByte();
                if (booleans[42]) _ = reader.ReadUInt16();
                if (booleans[43]) _ = reader.ReadByte();
                if (booleans[44]) _ = reader.ReadString();
                if (booleans[46]) _ = reader.ReadString();
                if (booleans[47]) _ = reader.ReadString();
                if (booleans[48]) _ = reader.ReadByte();
                if (booleans[49]) _ = reader.ReadByte();
                if (booleans[50]) _ = reader.ReadByte();

                if (booleans[51])
                {
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt32();
                }

                if (booleans[52]) _ = reader.ReadByte();
                if (booleans[53]) _ = reader.ReadString();

                if (booleans[54])
                {
                    _ = reader.ReadByte();
                    _ = reader.ReadByte();
                }

                if (booleans[56]) _ = reader.ReadByte();
                if (booleans[57]) _ = reader.ReadInt64();

                if (booleans[59])
                {
                    _ = reader.ReadByte();
                    _ = reader.ReadBoolean();
                }

                if (booleans[60]) _ = reader.ReadByte();
                if (booleans[65]) _ = reader.ReadByte();
                if (booleans[70]) _ = reader.ReadByte();
                if (booleans[71]) _ = reader.ReadByte();
                if (booleans[72]) _ = reader.ReadBytes(SortingOptions_ARRAY_SIZE);

                if (booleans[73])
                {
                    _ = reader.ReadByte();
                    _ = reader.ReadByte();
                }


                if (booleans[74]) _ = reader.ReadByte();
                if (booleans[76]) _ = reader.ReadByte();
                if (booleans[78]) _ = reader.ReadByte();
                if (booleans[79]) _ = reader.ReadByte();
                if (booleans[80]) _ = reader.ReadByte();

                if (booleans[81])
                {
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt16();
                }

                if (booleans[98]) _ = reader.ReadString();
                if (booleans[99]) _ = reader.ReadByte();
                if (booleans[100]) _ = reader.ReadByte();
                if (booleans[101]) _ = reader.ReadString();
                if (booleans[102]) _ = reader.ReadByte();
                if (booleans[105]) _ = reader.ReadString();

                if (booleans[106])
                {
                    var count106 = reader.ReadByte();
                    for (var i = 0; i < count106; i++)
                    {
                        _ = reader.ReadInt32();
                    }
                }

                if (booleans[107]) _ = reader.ReadString();
                if (booleans[108]) _ = reader.ReadString();
                if (booleans[109]) _ = reader.ReadByte();
                if (booleans[110]) _ = reader.ReadInt64();
                if (booleans[111]) _ = reader.ReadByte();

                if (booleans[112])
                {
                    var count112 = ReadVariableLength(reader);
                    for (var i = 0; i < count112; i++)
                    {
                        _ = reader.ReadString();
                        _ = reader.ReadString();
                        _ = reader.ReadString();
                        _ = reader.ReadByte();
                    }
                }

                if (booleans[113]) _ = reader.ReadByte();
                if (booleans[114]) _ = reader.ReadString();
                if (booleans[115]) _ = reader.ReadByte();

                if (booleans[116])
                {
                    var windowCount = ReadVariableLength(reader);
                    for (var i = 0; i < windowCount; i++)
                    {
                        var screenCount = ReadVariableLength(reader);
                        for (var j = 0; j < screenCount; j++)
                        {
                            var rectangleCount = ReadVariableLength(reader);
                            for (var k = 0; k < rectangleCount; k++)
                            {
                                _ = reader.ReadInt32();
                                _ = reader.ReadInt32();
                                _ = reader.ReadInt32();
                                _ = reader.ReadInt32();
                            }
                        }
                    }
                }

                if (booleans[119]) _ = reader.ReadByte();
                if (booleans[122]) _ = reader.ReadByte();
                if (booleans[123]) _ = reader.ReadByte();
                if (booleans[124]) _ = reader.ReadByte();
                if (booleans[127]) _ = reader.ReadByte();
                if (booleans[128]) _ = reader.ReadByte();
                if (booleans[129]) _ = reader.ReadByte();
                if (booleans[132]) _ = reader.ReadByte();
                if (booleans[133]) _ = reader.ReadByte();

                // Dat
                var datCount = reader.ReadUInt16();
                var datPaths = new Dictionary<uint, string>();
                for (var i = 0; i < datCount; i++)
                {
                    var id = reader.ReadUInt16();
                    var path = reader.ReadString();
                    _ = reader.ReadByte();
                    datPaths.Add(id, path);
                }

                // Gfx
                var gfxCount = reader.ReadUInt16();
                for (var i = 0; i < gfxCount; i++)
                {
                    _ = reader.ReadUInt16();
                    _ = reader.ReadString();
                    _ = reader.ReadByte();
                }

                // Dat2
                var dat2Count = reader.ReadUInt16();
                for (var i = 0; i < dat2Count; i++)
                {
                    _ = reader.ReadUInt16();
                    _ = reader.ReadString();
                    _ = reader.ReadByte();
                }

                // Accounts
                var accountCount = reader.ReadUInt16();
                for (var i = 0; i < accountCount; i++)
                {
                    var type = reader.ReadByte();
                    var uid = reader.ReadUInt16();
                    var accountName = reader.ReadString();
                    var windowsAccount = reader.ReadString();
                    var createdUtc = DateTime.FromBinary(reader.ReadInt64());
                    var lastUsedUtc = DateTime.FromBinary(reader.ReadInt64());
                    var totalUses = reader.ReadUInt16();
                    var arguments = reader.ReadString();

                    var pathToLoginDat = datPaths.ContainsKey(uid) ? datPaths[uid] : null;
                    IAccount? account = null;
                    if (File.Exists(pathToLoginDat))
                    {
                        account = Accounts!.FirstOrDefault(a => string.Equals(a.Name, accountName, StringComparison.OrdinalIgnoreCase));
                        if (account != null)
                        {
                            account.LoginFilePath = pathToLoginDat;
                        }
                        else
                        {
                            var newAccount = new Account(accountName, null, pathToLoginDat);
                            Items.Add(newAccount);
                            account = newAccount;
                        }

                        if (lastUsedUtc > account.LastLogin) account.LastLogin = lastUsedUtc;
                    }

                    var accountFlags = ExpandBooleans(reader.ReadBytes(reader.ReadByte()));

                    if (accountFlags[1]) _ = reader.ReadByte();
                    if (accountFlags[3]) _ = reader.ReadByte();
                    if (accountFlags[4]) _ = reader.ReadByte();
                    if (accountFlags[5]) _ = reader.ReadByte();

                    if (accountFlags[6])
                    {
                        var runAfterCount = ReadVariableLength(reader);
                        for (var j = 0; j < runAfterCount; j++)
                        {
                            _ = reader.ReadString();
                            _ = reader.ReadString();
                            _ = reader.ReadString();
                            _ = reader.ReadByte();
                        }
                    }


                    if (accountFlags[7]) _ = reader.ReadString();

                    if (accountFlags[8])
                    {
                        var length = reader.ReadUInt16();
                        if (length > 0)
                        {
                            _ = reader.ReadBytes(length);
                            _ = reader.ReadUInt16();
                        }
                    }

                    if (accountFlags[9]) _ = reader.ReadString();

                    if (accountFlags[10])
                    {
                        _ = reader.ReadString();
                        _ = reader.ReadByte();
                    }


                    if (accountFlags[12]) _ = reader.ReadString();

                    if (accountFlags[13])
                    {
                        var pageDataCount = reader.ReadByte();
                        for (var j = 0; j < pageDataCount; j++)
                        {
                            _ = reader.ReadByte();
                            _ = reader.ReadUInt16();
                            _ = reader.ReadBoolean();
                        }
                    }

                    if (accountFlags[14]) _ = reader.ReadBytes(reader.ReadByte());

                    if (accountFlags[16])
                    {
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                    }

                    if (accountFlags[20]) _ = reader.ReadByte();
                    if (accountFlags[21]) _ = reader.ReadByte();
                    if (accountFlags[22]) _ = reader.ReadByte();
                    if (accountFlags[23]) _ = reader.ReadByte();
                    if (accountFlags[24]) _ = reader.ReadInt64();

                    if (accountFlags[25])
                    {
                        var length = ReadVariableLength(reader);
                        if (length > 0)
                        {
                            _ = reader.ReadUInt16();
                            _ = reader.ReadInt64();
                            _ = reader.ReadBoolean();
                        }
                    }


                    if (accountFlags[26]) _ = reader.ReadInt32();

                    if (accountFlags[27])
                    {
                        if (reader.ReadByte() == 1) // File
                        {
                           _ = reader.ReadString();
                        }
                    }

                    if (accountFlags[28]) _ = reader.ReadUInt16();


                    var gwAccountFlags = ExpandBooleans(reader.ReadBytes(reader.ReadByte()));
                    if (type == 1) // GW1
                    {
                        if (gwAccountFlags[0]) _ = reader.ReadUInt16();
                        if (gwAccountFlags[1]) _ = reader.ReadString();
                    }
                    else // GW2
                    {
                        if (gwAccountFlags[0]) _ = reader.ReadUInt16();
                        if (gwAccountFlags[1]) _ = reader.ReadUInt16();

                        if (gwAccountFlags[3])
                        {
                            var apiKey = reader.ReadString();
                            if (account != null) account.ApiKey = apiKey;
                        }

                        if (accountFlags[4])
                        {
                            var booleansApi = ExpandBooleans(reader.ReadBytes(reader.ReadByte()));
                            if (booleansApi[0])
                            {
                                _ = reader.ReadInt64();
                                _ = reader.ReadByte();
                                _ = reader.ReadUInt16();
                            }

                            if (booleansApi[1])
                            {
                                _ = reader.ReadInt64();
                                _ = reader.ReadByte();
                                _ = reader.ReadInt32();
                            }
                        }

                        if (accountFlags[5]) _ = reader.ReadUInt16();
                        if (accountFlags[6]) _ = reader.ReadInt64();
                        if (accountFlags[7]) _ = reader.ReadString();
                    }

                }
            } 
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading accounts from {launcherFolder}");
        }
        finally
        {
            semaphore.Release();
            if (Any())
            {
                Loaded?.Invoke(this, EventArgs.Empty);
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public static Dictionary<string, List<IAccount>> AccountsByVpn(List<IAccount> accounts, bool ignoreVpn)
    {
        if (ignoreVpn)
        {
            return new Dictionary<string, List<IAccount>> {{"", accounts}};
        }

        var vpnAccounts = accounts
            .Where(a => a.HasVpn)
            .SelectMany(a => a.Vpns!, (a, vpn) => new { vpn, a })
            .GroupBy(t => t.vpn, t => t.a)
            .ToDictionary(g => g.Key, g => g.ToList());

        var nonVpnAccounts = accounts.Where(a => !a.HasVpn).ToList();
        if (nonVpnAccounts.Any()) vpnAccounts.Add("", nonVpnAccounts);

        return vpnAccounts;
    }

    public void SetUndo()
    {
        if (Accounts == null) return;
        foreach (var account in Accounts)
        {
            account.SetUndo();
        }
    }

    public bool Any() => Accounts?.Any() ?? false;
}