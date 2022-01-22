using System.Text.Json.Serialization;

namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VpnDetails
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private Settings? settings;

    public string Id { get; set; }
    public string ConnectionName { get; set; }
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

    public async Task<bool> Connect(CancellationToken cancellationToken)
    {
        if (!IsReal) return true;

        Logger.Info($"Connecting to VPN {ToString()}");
        var psi = new ProcessStartInfo("rasdial", $@"""{ConnectionName}""")
        {
            CreateNoWindow = true,
        };
        var vpnProcess = Process.Start(psi);
        if (vpnProcess == null)
        {
            Logger.Info($"Connecting from VPN {ToString()} Process null");
            return false;
        }
        await vpnProcess.WaitForExitAsync(cancellationToken);
        if (vpnProcess.ExitCode > 0 && vpnProcess.ExitCode != 703)
        {
            Logger.Info($"Connecting to VPN {ToString()} Error={vpnProcess.ExitCode}");
            return false;
        }
        await Task.Delay(new TimeSpan(0, 0, 4), cancellationToken);
        return true;
    }

    public async Task<bool> Disconnect(CancellationToken cancellationToken)
    {
        if (!IsReal) return true;

        Logger.Info($"Disconnecting from VPN {ToString()}");
        var psi = new ProcessStartInfo("rasdial", $@"""{ConnectionName}"" /d")
        {
            CreateNoWindow = true,
        };
        var vpnProcess = Process.Start(psi);
        if (vpnProcess == null)
        {
            Logger.Info($"Disconnecting from VPN {ToString()} Process null");
            return false;
        }
        await vpnProcess.WaitForExitAsync(cancellationToken);
        if (vpnProcess.ExitCode > 0)
        {
            Logger.Info($"Disconnecting from VPN {ToString()} Error={vpnProcess.ExitCode}");
            return false;
        }
        await Task.Delay(new TimeSpan(0, 0, 4), cancellationToken);
        return true;
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