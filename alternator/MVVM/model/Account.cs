namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class Account : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private string name;
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    private string? character;
    public string? Character
    {
        get => character;
        set => SetProperty(ref character, value);
    }

    private string loginFilePath;
    public string LoginFilePath
    {
        get => loginFilePath;
        set => SetProperty(ref loginFilePath, value);
    }

    private DateTime lastLogin;
    public DateTime LastLogin
    {
        get => lastLogin;
        set => SetProperty(ref lastLogin, value);
    }

    private DateTime lastCollection;
    public DateTime LastCollection
    {
        get => lastCollection;
        set => SetProperty(ref lastCollection, value);
    }

    private DateTime createdAt;
    public DateTime CreatedAt
    {
        get => createdAt;
        set => SetProperty(ref createdAt, value);
    }

    [NonSerialized] public FileInfo LoginFile;

    public Account(string name, string? character, string loginFilePath)
    {
        Name = name;
        Character = character;
        LoginFilePath = loginFilePath;

        LastLogin = DateTime.MinValue;
        LastCollection = DateTime.MinValue;
        CreatedAt = DateTime.Now;
        LoginFile = new FileInfo(loginFilePath);
    }

    private string DebugDisplay => $"{Name} ({Character}) {LastLogin} {LastCollection}";
    public Client Client { get; set; }

    private void SwapLogin(FileInfo gw2LocalDat)
    {
        if (gw2LocalDat.Exists)
        {
            if (gw2LocalDat.LinkTarget != null)
            {
                gw2LocalDat.Delete();
            }
            else
            {
                File.Move(gw2LocalDat.FullName, $"{gw2LocalDat.FullName}.bak", true);
            }
        }

        // Symbolic link creation requires process to be Admin
        gw2LocalDat.CreateAsSymbolicLink(LoginFile.FullName);
        Logger.Debug("{0} dat file linked to: {1}", Name, LoginFile.FullName);
    }

    public async Task SwapLoginAsync(FileInfo gw2LocalDat)
    {
        await Task.Run(() =>
        {
            SwapLogin(gw2LocalDat);
        });
        await Task.Delay(200);
    }
}