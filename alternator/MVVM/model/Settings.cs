namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
public class Settings : ObservableObject
{

    private string? gw2Folder;
    public string Gw2Folder
    {
        get => gw2Folder;
        set => SetProperty(ref gw2Folder, value);
    }

    private int maxLoginInstances;
    public int MaxLoginInstances
    {
        get => maxLoginInstances;
        set => SetProperty(ref maxLoginInstances, value);
    }

    public Settings() : this(false) { }

    public Settings(bool setDefault)
    {
        if (setDefault)
        {

            var gcMemoryInfo = GC.GetGCMemoryInfo();
            var freeMemory = (int)((gcMemoryInfo.TotalAvailableMemoryBytes - gcMemoryInfo.MemoryLoadBytes) >> 20);
            maxLoginInstances = freeMemory / 1400;

            if (Directory.Exists(@"C:\Program Files (x86)\Guild Wars 2")) gw2Folder = @"C:\Program Files (x86)\Guild Wars 2";
        }
    }
}