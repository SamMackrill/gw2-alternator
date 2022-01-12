namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class Currency : ObservableObject
{
    private string name = null!;
    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }


    private int count;
    public int Count
    {
        get => count;
        set => SetProperty(ref count, value);
    }

    public Currency(string name, int count)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Count = count;
    }

    private string DebugDisplay => $"{Name}={Count}";

}