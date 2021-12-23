using System.Text.Json.Serialization;

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
        set
        {
            if (SetProperty(ref lastLogin, value))
            {
                OnPropertyChanged(nameof(LoginRequired));
            }
        }
    }

    private DateTime lastCollection;
    public DateTime LastCollection
    {
        get => lastCollection;
        set
        {
            if (SetProperty(ref lastCollection, value))
            {
                OnPropertyChanged(nameof(CollectionRequired));
            }
        }
    }

    private DateTime createdAt;
    public DateTime CreatedAt
    {
        get => createdAt;
        set
        {
            if (SetProperty(ref createdAt, value))
            {
                OnPropertyChanged(nameof(CollectionRequired));
            }
        }
    }


    public Account(string name, string? character, string loginFilePath)
    {
        Name = name;
        Character = character;
        LoginFilePath = loginFilePath;

        LastLogin = DateTime.MinValue;
        LastCollection = DateTime.MinValue;
        CreatedAt = DateTime.Now;

        Client = new Client(this);
    }

    private string DebugDisplay => $"{Name} ({Character}) {LastLogin} {LastCollection}";

    [field: NonSerialized]
    [JsonIgnore]
    public Client Client { get; set; }

    [JsonIgnore]
    public bool LoginRequired => LastLogin < DateTime.UtcNow.Date;

    [JsonIgnore]
    public bool CollectionRequired => LastCollection < DateTime.UtcNow.Date
                                      && DateTime.UtcNow.Date.Subtract(LastCollection).TotalDays > 30;

    [JsonIgnore]
    public bool UpdateRequired => true;

    private void LinkFiles(FileInfo from, FileInfo to)
    {
        if (from.Exists)
        {
            if (from.LinkTarget != null)
            {
                from.Delete();
            }
            else
            {
                File.Move(from.FullName, $"{from.FullName}.bak", true);
            }
        }

        // Symbolic link creation requires process to be Admin
        from.CreateAsSymbolicLink(to.FullName);
        Logger.Debug("{0} {1} file linked to: {2}", Name, to.Extension, to.FullName);
    }

    public async Task SwapFilesAsync(FileInfo gw2LocalDat, FileInfo gw2GfxSettings, FileInfo referenceGfxSettingsFile)
    {
        await Task.Run(() =>
        {
            LinkFiles(gw2LocalDat, new FileInfo(loginFilePath));
        });
        await Task.Run(() =>
        {
            LinkFiles(gw2GfxSettings, referenceGfxSettingsFile);
        });
        await Task.Delay(200);
    }
}