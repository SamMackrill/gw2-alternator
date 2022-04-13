namespace guildwars2.tools.alternator.MVVM.model;

[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class SuccessFailCounter
{
    public SuccessFailCounter()
    {
        calls = new List<DateTime>();
        fails = new List<DateTime>();
        successes = new List<DateTime>();
        consecutiveFails = new Counter();
        consecutiveSuccesses = new Counter();
    }

    public DateTime LastAttempt { get; private set; }

    public int CallCount => calls.Count;
    public int FailCount => fails.Count;
    public int SuccessCount => successes.Count;

    public int ConsecutiveFails => consecutiveFails.Count;
    public int ConsecutiveSuccesses => consecutiveSuccesses.Count;

    private readonly List<DateTime> calls;
    private readonly List<DateTime> fails;
    private readonly List<DateTime> successes;
    private Counter consecutiveFails;
    private Counter consecutiveSuccesses;

    public void SetAttempt()
    {
        LastAttempt = DateTime.UtcNow;
        calls.Add(LastAttempt);
    }

    public void SetSuccess()
    {
        successes.Add(DateTime.UtcNow);
        consecutiveFails = new Counter();
        consecutiveSuccesses.Increment();
    }

    public void SetFail()
    {
        fails.Add(DateTime.UtcNow);
        consecutiveFails.Increment();
        consecutiveSuccesses = new Counter();
    }

    public int RecentFails(int cutoff) => fails.Count(d => DateTime.UtcNow.Subtract(d).TotalSeconds <= cutoff);
    public int RecentSuccesses(int cutoff) => successes.Count(d => DateTime.UtcNow.Subtract(d).TotalSeconds <= cutoff);
    public int RecentCalls(int cutoff) => calls.Count(d => DateTime.UtcNow.Subtract(d).TotalSeconds <= cutoff);

    private string DebugDisplay => ToString();

    public override string ToString()
    {
        return $"SuccessFail: CC={CallCount} SC={SuccessCount} CS={ConsecutiveSuccesses} FC={FailCount} CF={ConsecutiveFails}";
    }
}