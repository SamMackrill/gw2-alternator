namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class Currency
{
    public string Name { get; set; }
    public int Count { get; set; }

    public Currency(string name, int count)
    {
        Name = name;
        Count = count;
    }

    private string DebugDisplay => $"{Name}={Count}";

}