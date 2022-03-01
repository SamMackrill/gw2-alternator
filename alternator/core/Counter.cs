namespace guildwars2.tools.alternator;

[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class Counter : ObservableObject
{
    private int count;
    public int Count => count;

    public void Increment()
    {
        Interlocked.Increment(ref count);
        OnPropertyChanged(nameof(Count));
    }

    private string DebugDisplay => count.ToString();

    public override string ToString() => DebugDisplay;
}