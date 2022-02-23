﻿namespace guildwars2.tools.alternator.MVVM.model;

public interface IJsonCollection
{
    bool Ready { get; set; }
    event EventHandler? Loaded;
    event EventHandler? LoadFailed;
    event EventHandler? Updated;
    Task Save();
    Task Load();
    event PropertyChangedEventHandler? PropertyChanged;
    event PropertyChangingEventHandler? PropertyChanging;
}

public abstract class JsonCollection<T> : ObservableObject, IJsonCollection
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

    public abstract event EventHandler? Loaded;
    public abstract event EventHandler? LoadFailed;
    public abstract event EventHandler? Updated;

    public abstract Task Save();
    public abstract Task Load();
}