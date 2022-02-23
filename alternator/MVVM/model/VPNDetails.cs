﻿using System.Net.Http;
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
    public int Delay => LaunchDelay();

    [JsonIgnore]
    public List<VpnConnectionMetrics> Connections { get; private set; }


    private SuccessFailCounter successFailCounter;

    public VpnDetails()
    {
        successFailCounter = new SuccessFailCounter();
        Connections = new List<VpnConnectionMetrics>();
    }

    private string DebugDisplay => ToString();

    [JsonIgnore]
    public bool IsReal => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(ConnectionName);

    public DateTime Available(DateTime cutoff)
    {
        var available = successFailCounter.LastAttempt.AddSeconds(Delay);
        Logger.Debug("{0} VPN offset={1}", DisplayId, available.Subtract(cutoff).TotalSeconds);
        return available > cutoff ? available : cutoff;
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
        if (status != null) return status;

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
            return $"{display} VPN Process null";
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
            return $"{display} VPN Error={vpnProcess.ExitCode}";
        }

        if (record) LastConnectionSuccess = DateTime.Now;
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
        successFailCounter.SetAttempt();
    }

    public void SetSuccess()
    {
        successFailCounter.SetSuccess();
        LastLoginSuccess = DateTime.Now;
    }

    public void SetFail(IAccount account)
    {
        Logger.Debug("{0} VPN SetFail by account {1}", DisplayId, account.Name);
        successFailCounter.SetFail();
        LastLoginFail = DateTime.Now;
        LastLoginFailAccount = account.Name;
        Cancellation.Cancel();
    }

    private int LaunchDelay()
    {
        var delay = BandDelay(successFailCounter.CallCount);

        if (successFailCounter.ConsecutiveFails > 0) delay = Math.Max(delay, 40 + 20 * successFailCounter.ConsecutiveFails);

        Logger.Debug("{0} VPN delay={1} ({2})", DisplayId, delay, successFailCounter.ToString());
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
    public int RecentFailures => successFailCounter.RecentFails(120);

    [JsonIgnore]
    public int RecentCalls => successFailCounter.RecentCalls(180);

    [JsonIgnore]
    public CancellationTokenSource Cancellation { get; set; }

    public int GetPriority(int accountsCount, int maxAccounts)
    {
        var real = IsReal ? maxAccounts : 0;
        var countPriority = (Math.Max(0, maxAccounts - accountsCount) + real) * maxAccounts;

        Priority = RecentCalls + countPriority;
        Logger.Debug("{0} VPN Priority={1} (R={2} C={3} CP={4} MA={5})", DisplayId, Priority, real, accountsCount, countPriority, maxAccounts);
        return Priority;
    }
}