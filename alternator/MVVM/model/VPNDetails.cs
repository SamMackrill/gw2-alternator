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
        var (launchDelay, why) = LaunchDelay(ignoreCalls);
        var available = accountSuccessFailCounter.LastAttempt.AddSeconds(launchDelay);
        if (vnpAvailable > available)
        {
            available = vnpAvailable;
            why = "VPN on hold";
        }

        if (available <= cutoff)
        {
            available = cutoff;
        }

        Logger.Debug("{0} available in={1}s because {2}", DisplayId, available.Subtract(DateTime.Now).TotalSeconds, why);
        return available;
    }

    private static readonly List<int> CriticalVpnErrors = new() { 809 };

    private DateTime VnpAvailable()
    {
        var failObsolete = DateTime.UtcNow.Subtract(LastConnectionFail).TotalHours >= 1;
        if (!failObsolete && CriticalVpnErrors.Contains(LastConnectionFailCode))
        {
            return DateTime.MaxValue;
        }

        return LastConnectionFail <= LastLoginSuccess ? DateTime.MinValue : LastConnectionFail.AddSeconds(30);
    }


    public bool Equals(VpnDetails? other)
    {
        return other != null && Id == other.Id;
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

    public async Task<string?> Disconnect()
    {
        currentConnectionMetrics?.DisconnectStart();

        var status = await RunRasdial("Disconnecting from", @" /d", false);
        if (status != null) return status;

        currentConnectionMetrics?.Disconnected();
        return null;
    }

    private async Task<string?> RunRasdial(string display, string arg, bool record, CancellationToken? cancellationToken = null)
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

        if (cancellationToken != null)
        {
            _ = Task.Run(() => ReadStream(vpnProcess.StandardOutput, s => Logger.Debug("VPN: {0}", s)), cancellationToken.Value);
            _ = Task.Run(() => ReadStream(vpnProcess.StandardError, s => Logger.Debug("VPN Error: {0}", s)), cancellationToken.Value);

            await vpnProcess.WaitForExitAsync(cancellationToken.Value);
        }
        else
        {
            _ = Task.Run(() => ReadStream(vpnProcess.StandardOutput, s => Logger.Debug("VPN: {0}", s)));
            _ = Task.Run(() => ReadStream(vpnProcess.StandardError, s => Logger.Debug("VPN Error: {0}", s)));

            await vpnProcess.WaitForExitAsync();
        }

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
        await Task.Delay(new TimeSpan(0, 0, 1));
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

    public void SetFail(IAccount account, bool cancelAll)
    {
        Logger.Debug("{0} SetFail by account {1}", DisplayId, account.Name);
        accountSuccessFailCounter.SetFail();
        LastLoginFail = DateTime.UtcNow;
        LastLoginFailAccount = account.Name;
        if (cancelAll) Cancellation?.Cancel($"{DisplayId} SetFail by account {account.Name}");
    }

    public (int, string) LaunchDelay(bool ignoreCalls)
    {
        var (delay, why) = ignoreCalls ? (0, "ignore calls") : BandDelay(accountSuccessFailCounter.CallCount);

        if (accountSuccessFailCounter.ConsecutiveFails > 0)
        {
            var failDelay = 40 + 20 * accountSuccessFailCounter.ConsecutiveFails;
            if (failDelay > delay)
            {
                delay = failDelay;
                why = $"fail count = {accountSuccessFailCounter.ConsecutiveFails}";
            }
        }

        if (delay == 0) why += " no fails";

        Logger.Debug("{0} delay={1} ({2}) because {3}", DisplayId, delay, accountSuccessFailCounter.ToString(), why);
        return (delay, why);
    }

    private (int, string) BandDelay(int count)
    {
        if (settings==null) return (0, "no settings!");

        if (count < settings.AccountBand1) return (settings.AccountBand1Delay, $"Band 1 (count={count})");
        if (count < settings.AccountBand2) return (settings.AccountBand2Delay, $"Band 2 (count={count})");
        if (count < settings.AccountBand3) return (settings.AccountBand3Delay, $"Band 3 (count={count})");
        return (settings.AccountBand3Delay + 60, $"Band > 3 (count={count})");
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

        status = await Disconnect();
        if (status != null) return status;

        return $"OK IP={pubIp}";
    }

    [JsonIgnore]
    public string DisplayId => $"{(string.IsNullOrWhiteSpace(Id) ? "No" : Id)} VPN";

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
        Logger.Debug("{0} Priority={1} (R={2} C={3} CP={4} MA={5})", DisplayId, Priority, real, accountsCount, countPriority, maxAccounts);
        return Priority;
    }
}