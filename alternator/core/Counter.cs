using System.Threading;

namespace guildwars2.tools.alternator;

public class Counter
{
    private int count;

    public int Count => count;

    public void Increment()
    {
        Interlocked.Increment(ref count);
    }
}