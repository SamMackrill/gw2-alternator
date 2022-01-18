namespace guildwars2.tools.alternator.MVVM.model;

public abstract class JsonCollection<T> : ObservableObject
{
    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    protected readonly string vpnJson;
    protected readonly SemaphoreSlim semaphore;

    protected JsonCollection(FileSystemInfo folderPath, string jsonFile)
    {
        vpnJson = Path.Combine(folderPath.FullName, jsonFile);
        semaphore = new SemaphoreSlim(1, 1);
    }

    protected List<T>? Items { get; set; }

    public bool Ready { get; set; }

    public delegate Task AsyncEventHandler<in TEventArgs>(object? sender, TEventArgs e);

    public abstract event AsyncEventHandler<EventArgs>? Loaded;
    public abstract event AsyncEventHandler<EventArgs>? LoadFailed;

    public abstract Task Save();
    public abstract Task Load();
}