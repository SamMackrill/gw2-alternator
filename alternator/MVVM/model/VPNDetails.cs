using System.Text.Json.Serialization;

namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VpnDetails
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private Settings? settings;

    public string Id { get; set; }
    public string ConnectionName { get; }
    public DateTime LastLoginFail { get; set; }
    public DateTime LastLoginSuccess { get; set; }

    [field: NonSerialized]
    [JsonIgnore]
    public Counter CallCount { get; private set; }
    
    [field: NonSerialized]
    [JsonIgnore]
    public Counter FailCount { get; private set; }

    [field: NonSerialized]
    [JsonIgnore]
    public Counter SuccessCount { get; private set; }

    [field: NonSerialized]
    [JsonIgnore]
    public Counter ConsecutiveFailedCount { get; private set; }

    public int Delay => LaunchDelay();


    public VpnDetails()
    {
        CallCount = new Counter();
        FailCount = new Counter();
        SuccessCount = new Counter();
        ConsecutiveFailedCount = new Counter();
    }

    private string DebugDisplay => ToString();
    public bool IsReal => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(ConnectionName);
    public DateTime Available => LastLoginSuccess.AddSeconds(Delay);

    public override string ToString() => $"{Id} \"{ConnectionName}\"";

    public async Task Connect(CancellationToken cancellationToken)
    {
        if (!IsReal) return;

        Logger.Info($"Connecting to VPN {ToString()}");
        var vpnProcess = Process.Start("rasdial", $@"""{ConnectionName}""");
        await vpnProcess.WaitForExitAsync(cancellationToken);
        await Task.Delay(new TimeSpan(0, 0, 4), cancellationToken);
    }

    public async Task Disconnect(CancellationToken cancellationToken)
    {
        if (!IsReal) return;

        Logger.Info($"Disconnecting from VPN {ToString()}");
        var vpnProcess = Process.Start("rasdial", $@"""{ConnectionName}"" /d");
        await vpnProcess.WaitForExitAsync(cancellationToken);
        await Task.Delay(new TimeSpan(0, 0, 4), cancellationToken);
    }

    public void SetAttempt(Settings settings)
    {
        this.settings = settings;
        CallCount.Increment();
    }

    public void SetSuccess()
    {
        SuccessCount.Increment();
        LastLoginSuccess = DateTime.Now;
    }

    public void SetFail()
    {
        FailCount.Increment();
        SuccessCount = new Counter();
        LastLoginFail = DateTime.Now;
    }

    private int LaunchDelay()
    {
        var delay = BandDelay(CallCount.Count);

        if (FailCount.Count > 0) delay = Math.Max(delay, 60);
        //if (clientFailedCount > 2) delay = Math.Max(delay, 60 * (clientFailedCount - 2));
        if (ConsecutiveFailedCount.Count > 0) delay = Math.Max(delay, 60 * ConsecutiveFailedCount.Count);

        return delay;
    }

    private int BandDelay(int count)
    {
        if (settings==null) return 0;

        if (count < settings.AccountBand1) return settings.AccountBand1Delay;
        if (count < settings.AccountBand2) return settings.AccountBand2Delay;
        if (count < settings.AccountBand3) return settings.AccountBand3Delay;
        return settings.AccountBand3Delay + 60;
    }
}