using System.Net;
using System.Text.Json.Serialization;

namespace guildwars2.tools.alternator.MVVM.model;


public class EventMetrics
{
    public DateTime StartAt { get; }
    public DateTime FinishAt { get; private set; }
    public EventMetrics() => StartAt = DateTime.Now;
    public void Done() => FinishAt = DateTime.Now;
    public TimeSpan Duration => FinishAt.Subtract(StartAt);
}

public class VpnConnectionMetrics
{
    public EventMetrics? ConnectMetrics { get; }
    public EventMetrics? DisconnectMetrics { get; private set; }

    public VpnConnectionMetrics() => ConnectMetrics = new EventMetrics();
    public void Connected() => ConnectMetrics?.Done();
    public void DisconnectStart() => DisconnectMetrics = new EventMetrics();
    public void Disconnected() => DisconnectMetrics?.Done();
}

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VpnDetails : IEquatable<VpnDetails>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private Settings? settings;

    public string Id { get; set; }
    public string ConnectionName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime LastConnectionFail { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime LastConnectionSuccess { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime LastLoginFail { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime LastLoginSuccess { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int LastConnectionFailCode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string LastLoginFailAccount { get; set; }

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

    [JsonIgnore]
    public int Delay => LaunchDelay();

    [JsonIgnore]
    public List<VpnConnectionMetrics> Connections { get; private set; }

    public VpnDetails()
    {
        CallCount = new Counter();
        FailCount = new Counter();
        SuccessCount = new Counter();
        ConsecutiveFailedCount = new Counter();
        Connections = new List<VpnConnectionMetrics>();
    }

    private string DebugDisplay => ToString();

    [JsonIgnore]
    public bool IsReal => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(ConnectionName);

    public DateTime Available(DateTime cutoff)
    {
        var available = LastLoginSuccess.AddSeconds(Delay);
        return available > cutoff ? available : cutoff;
    }

    public bool Equals(VpnDetails? other)
    {
        if (other == null) return false;
        return Id == other.Id;
    }

    public override string ToString() => $"{Id} \"{ConnectionName}\"";

    private VpnConnectionMetrics currentConnectionMetrics;

    public async Task<bool> Connect(CancellationToken cancellationToken)
    {
        currentConnectionMetrics = new VpnConnectionMetrics();
        Connections.Add(currentConnectionMetrics);
        if (!await RunRasdial("Connecting to", "", true, cancellationToken)) return false;
        currentConnectionMetrics.Connected();

        string hostName = Dns.GetHostName();
        var hostEntry = await Dns.GetHostEntryAsync(hostName, cancellationToken);

        return true;
    }

    public async Task<bool> Disconnect(CancellationToken cancellationToken)
    {
        currentConnectionMetrics.DisconnectStart();
        if (!await RunRasdial("Disconnecting from", @" /d", false, cancellationToken)) return false;
        currentConnectionMetrics.Disconnected();
        return true;
    }

    private async Task<bool> RunRasdial(string display, string arg, bool record, CancellationToken cancellationToken)
    {
        if (!IsReal) return true;

        Logger.Info($"{display} VPN {ToString()}");
        var psi = new ProcessStartInfo("rasdial", $@"""{ConnectionName}""{arg}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var vpnProcess = Process.Start(psi);
        if (vpnProcess == null)
        {
            Logger.Error($"{display} VPN {ToString()} Process null");
            if (record)
            {
                LastConnectionFail = DateTime.Now;
                LastConnectionFailCode = -999;
            }
            return false;
        }

        _ = Task.Run(() => ReadStream(vpnProcess.StandardOutput, s => Logger.Debug($"VPN: {s}")), cancellationToken);
        _ = Task.Run(() => ReadStream(vpnProcess.StandardError, s => Logger.Debug($"VPN Error: {s}")), cancellationToken);

        await vpnProcess.WaitForExitAsync(cancellationToken);
        if (vpnProcess.ExitCode > 0)
        {
            Logger.Error($"{display} VPN {ToString()} Error={vpnProcess.ExitCode}");
            if (record)
            {
                LastConnectionFail = DateTime.Now;
                LastConnectionFailCode = vpnProcess.ExitCode;
            }
            return false;
        }

        if (record) LastConnectionSuccess = DateTime.Now;
        await Task.Delay(new TimeSpan(0, 0, 1), cancellationToken);
        return true;
    }

    private void ReadStream(TextReader textReader, Action<string> callback)
    {
        while (true)
        {
            var line = textReader.ReadLine();
            if (line == null) break;

            callback(line);
        }
    }

    public void SetAttempt(Settings currentSettings)
    {
        settings = currentSettings;
        CallCount.Increment();
    }

    public void SetSuccess()
    {
        SuccessCount.Increment();
        LastLoginSuccess = DateTime.Now;
    }

    public void SetFail(IAccount account)
    {
        FailCount.Increment();
        SuccessCount = new Counter();
        LastLoginFail = DateTime.Now;
        LastLoginFailAccount = account.Name;
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