using System.Text.Json.Serialization;

namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VpnDetails
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public string Id { get; set; }
    public string ConnectionName { get; set; }
    public DateTime LastLoginFail { get; set; }
    public DateTime LastLoginSuccess { get; set; }

    [field: NonSerialized]
    [JsonIgnore]
    public Counter CallCount { get; set; }
    
    [field: NonSerialized]
    [JsonIgnore]
    public Counter ErrorCount { get; set; }

    [field: NonSerialized]
    [JsonIgnore]
    public Counter SuccessCount { get; set; }

    public int Delay => 60;


    public VpnDetails()
    {
        CallCount = new Counter();
        ErrorCount = new Counter();
        SuccessCount = new Counter();
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

    public void SetAttempt()
    {
        CallCount.Increment();
    }

    public void SetSuccess()
    {
        SuccessCount.Increment();
        LastLoginSuccess = DateTime.Now;
    }

    public void SetFail()
    {
        ErrorCount.Increment();
        SuccessCount = new Counter();
        LastLoginFail = DateTime.Now;
    }
}