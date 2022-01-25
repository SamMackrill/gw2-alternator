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
    public DateTime LastConnectionFail { get; set; }
    public DateTime LastConnectionSuccess { get; set; }
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

    [JsonIgnore]
    public int Delay => LaunchDelay();


    public VpnDetails()
    {
        CallCount = new Counter();
        FailCount = new Counter();
        SuccessCount = new Counter();
        ConsecutiveFailedCount = new Counter();
    }

    private string DebugDisplay => ToString();

    [JsonIgnore]
    public bool IsReal => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(ConnectionName);

    [JsonIgnore]
    public DateTime Available => LastLoginSuccess.AddSeconds(Delay);

    public override string ToString() => $"{Id} \"{ConnectionName}\"";

    public async Task<bool> Connect(CancellationToken cancellationToken)
    {
        return await RunRasdial("Connecting to", "", cancellationToken);
    }

    public async Task<bool> Disconnect(CancellationToken cancellationToken)
    {
        return await RunRasdial("Disconnecting from", @" /d", cancellationToken);
    }

    private async Task<bool> RunRasdial(string display, string arg, CancellationToken cancellationToken)
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
            LastConnectionFail = DateTime.Now;
            return false;
        }

        _ = Task.Run(() => ReadStream(vpnProcess.StandardOutput, s => Logger.Debug($"VPN: {s}")), cancellationToken);
        _ = Task.Run(() => ReadStream(vpnProcess.StandardError, s => Logger.Debug($"VPN Error: {s}")), cancellationToken);

        await vpnProcess.WaitForExitAsync(cancellationToken);
        if (vpnProcess.ExitCode > 0)
        {
            Logger.Error($"{display} VPN {ToString()} Error={vpnProcess.ExitCode}");
            LastConnectionFail = DateTime.Now;
            return false;
        }

        LastConnectionSuccess = DateTime.Now;
        await Task.Delay(new TimeSpan(0, 0, 4), cancellationToken);
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