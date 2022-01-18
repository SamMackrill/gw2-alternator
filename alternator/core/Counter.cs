namespace guildwars2.tools.alternator;

public class Counter : ObservableObject
{
    private int count;

    public int Count => count;

    public void Increment()
    {
        Interlocked.Increment(ref count);
        OnPropertyChanged(nameof(Count));
    }
}