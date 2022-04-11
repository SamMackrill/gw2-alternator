using System.Net.Http;
using System.Text.Json.Serialization;

namespace guildwars2.tools.alternator.MVVM.model;

public class EventMetrics
{
    public DateTime StartAt { get; }
    public DateTime FinishAt { get; private set; }
    public EventMetrics() => StartAt = DateTime.UtcNow;
    public void Done() => FinishAt = DateTime.UtcNow;
    public TimeSpan Duration => FinishAt.Subtract(StartAt);
}

public class VpnConnectionMetrics
{
    public string? Id { get; }

    public EventMetrics? ConnectMetrics { get; }
    public EventMetrics? DisconnectMetrics { get; private set; }

    public VpnConnectionMetrics(string? id)
    {
        Id = id;
        ConnectMetrics = new EventMetrics();
    }

    public void Connected() => ConnectMetrics?.Done();
    public void DisconnectStart() => DisconnectMetrics = new EventMetrics();
    public void Disconnected() => DisconnectMetrics?.Done();
}

[Serializable]
[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VpnDetails : ObservableObject, IEquatable<VpnDetails>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private Settings? settings;

    private string? id;
    public string? Id
    {
        get => id;
        set => SetProperty(ref id, value);
    }

    private string? connectionName;
    public string? ConnectionName
    {
        get => connectionName;
        set => SetProperty(ref connectionName, value);
    }

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
    public string? LastLoginFailAccount { get; set; }


    [field: NonSerialized]
    [JsonIgnore]
    public int Priority { get; private set; }

    [JsonIgnore]
    public List<VpnConnectionMetrics> Connections { get; private set; }


    private SuccessFailCounter accountSuccessFailCounter;

    public VpnDetails()
    {
        accountSuccessFailCounter = new SuccessFailCounter();
        Connections = new List<VpnConnectionMetrics>();
    }

    private string DebugDisplay => ToString();

    [JsonIgnore]
    public bool IsReal => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(ConnectionName);

    public DateTime Available(DateTime cutoff, bool ignoreCalls)
    {
        var vnpAvailable = VnpAvailable();
        var accountAvailable = accountSuccessFailCounter.LastAttempt.AddSeconds(LaunchDelay(ignoreCalls));
        var available = vnpAvailable > accountAvailable ? vnpAvailable : accountAvailable;
        Logger.Debug("{0} VPN offset={1}", DisplayId, available.Subtract(cutoff).TotalSeconds);
        return available > cutoff ? available : cutoff;
    }

    private static readonly List<int> CriticalVpnErrors = new() { 809 };

    private DateTime VnpAvailable()
    {
        if (DateTime.UtcNow.Subtract(LastConnectionFail).TotalHours < 1 && CriticalVpnErrors.Contains(LastConnectionFailCode))
        {
            return DateTime.MaxValue;
        }

        if (LastConnectionFail > LastLoginSuccess) return LastConnectionFail.AddSeconds(30);

        return DateTime.MinValue;
    }


    public bool Equals(VpnDetails? other)
    {
        if (other == null) return false;
        return Id == other.Id;
    }

    public override string ToString() => $"{Id} \"{ConnectionName}\"";

    private VpnConnectionMetrics? currentConnectionMetrics;

    public async Task<string?> Connect(CancellationToken cancellationToken)
    {
        currentConnectionMetrics = new VpnConnectionMetrics(Id);
        Connections.Add(currentConnectionMetrics);

        var status = await RunRasdial("Connecting to", "", true, cancellationToken);
        if (status != null)
        {
            return status;
        }

        currentConnectionMetrics.Connected();
        return null;
    }

    public async Task<string?> Disconnect(CancellationToken cancellationToken)
    {
        currentConnectionMetrics?.DisconnectStart();

        var status = await RunRasdial("Disconnecting from", @" /d", false, cancellationToken);
        if (status != null) return status;

        currentConnectionMetrics?.Disconnected();
        return null;
    }

    private async Task<string?> RunRasdial(string display, string arg, bool record, CancellationToken cancellationToken)
    {
        if (!IsReal) return null;

        Logger.Info("{0} VPN {1}", display, ToString());
        var psi = new ProcessStartInfo("rasdial", $@"""{ConnectionName}""{arg}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var vpnProcess = Process.Start(psi);
        if (vpnProcess == null)
        {
            Logger.Error("{0} VPN {1} Process null", display, ToString());
            if (record)
            {
                LastConnectionFail = DateTime.UtcNow;
                LastConnectionFailCode = -999;
            }
            return $"{display} VPN Process null";
        }

        _ = Task.Run(() => ReadStream(vpnProcess.StandardOutput, s => Logger.Debug("VPN: {0}", s)), cancellationToken);
        _ = Task.Run(() => ReadStream(vpnProcess.StandardError, s => Logger.Debug("VPN Error: {0}", s)), cancellationToken);

        await vpnProcess.WaitForExitAsync(cancellationToken);
        if (vpnProcess.ExitCode > 0)
        {
            Logger.Error("{0} VPN {1} Error={2}", display, ToString(), vpnProcess.ExitCode);
            if (record)
            {
                LastConnectionFail = DateTime.UtcNow;
                LastConnectionFailCode = vpnProcess.ExitCode;
            }
            return $"{display} VPN Error={vpnProcess.ExitCode}";
        }

        if (record) LastConnectionSuccess = DateTime.UtcNow;
        await Task.Delay(new TimeSpan(0, 0, 1), cancellationToken);
        return null;
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
        accountSuccessFailCounter.SetAttempt();
    }

    public void SetSuccess()
    {
        accountSuccessFailCounter.SetSuccess();
        LastLoginSuccess = DateTime.UtcNow;
    }

    public void SetFail(IAccount account)
    {
        Logger.Debug("{0} VPN SetFail by account {1}", DisplayId, account.Name);
        accountSuccessFailCounter.SetFail();
        LastLoginFail = DateTime.UtcNow;
        LastLoginFailAccount = account.Name;
        Cancellation?.Cancel();
    }

    public int LaunchDelay(bool ignoreCalls)
    {
        var delay = ignoreCalls ? 0 : BandDelay(accountSuccessFailCounter.CallCount);

        if (accountSuccessFailCounter.ConsecutiveFails > 0) delay = Math.Max(delay, 40 + 20 * accountSuccessFailCounter.ConsecutiveFails);

        Logger.Debug("{0} VPN delay={1} ({2})", DisplayId, delay, accountSuccessFailCounter.ToString());
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

    public void Undo()
    {
        throw new NotImplementedException();
    }

    public async Task<string> Test(CancellationToken token)
    {
        var status = await Connect(token);
        if (status != null) return status;

        var webClient = new HttpClient();
        var pubIp = await webClient.GetStringAsync("https://api.ipify.org", token);

        status = await Disconnect(token);
        if (status != null) return status;

        return $"OK IP={pubIp}";
    }

    [JsonIgnore]
    public string DisplayId => string.IsNullOrWhiteSpace(Id) ? "No" : Id;

    [JsonIgnore]
    public int RecentFailures => accountSuccessFailCounter.RecentFails(120);

    [JsonIgnore]
    public int RecentCalls => accountSuccessFailCounter.RecentCalls(180);

    [JsonIgnore]
    public CancellationTokenSource? Cancellation { get; set; }

    public int GetPriority(int accountsCount, int maxAccounts)
    {
        var real = IsReal ? maxAccounts : 0;
        var countPriority = (Math.Max(0, maxAccounts - accountsCount) + real) * maxAccounts;

        Priority = RecentCalls + countPriority;
        Logger.Debug("{0} VPN Priority={1} (R={2} C={3} CP={4} MA={5})", DisplayId, Priority, real, accountsCount, countPriority, maxAccounts);
        return Priority;
    }
}