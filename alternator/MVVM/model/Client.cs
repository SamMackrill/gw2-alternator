namespace guildwars2.tools.alternator.MVVM.model;

public class Client : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string MutexName = "AN-Mutex-Window-Guild Wars 2";

    private readonly Account account;
    private Process? p;
    private long lastMemoryUsage;


    private DateTime startTime;
    public DateTime StartTime
    {
        get => startTime;
        set => SetProperty(ref startTime, value);
    }

    public DateTime ExitTime => p?.ExitTime ?? DateTime.MinValue;

    private RunState runStatus;
    public RunState RunStatus
    {
        get => runStatus;
        set
        {
            if (SetProperty(ref runStatus, value) && runStatus != RunState.Error) StatusMessage = null;
        }
    }

    private string? statusMessage;
    public string? StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public Client(Account account)
    {
        this.account = account;
        RunStatus = RunState.Ready;
    }

    public bool Start(LaunchType launchType, string gw2Location)
    {
        // Run gw2 exe with arguments
        var gw2Arguments = launchType is LaunchType.Update ? "-image" : $"-autologin -windowed -nosound -shareArchive -maploadinfo -dx9 -fps 20"; // -dat \"{account.LoginFile}\""
        var pi = new ProcessStartInfo(Path.Combine(gw2Location, "Gw2-64.exe"))
        {
            CreateNoWindow = true,
            Arguments = gw2Arguments, 
            UseShellExecute = false,
            WorkingDirectory = gw2Location,
        };
        p = new Process { StartInfo = pi };
        p.EnableRaisingEvents = true;
        p.Exited += Exited;

        _ = p.Start();
        RunStatus = RunState.Running;
        StartTime = p.StartTime;
        Logger.Debug("{0} Started {1}", account.Name, launchType);

        if (launchType is LaunchType.Update) return true;

        p.WaitForInputIdle();
        if (KillMutex()) return true;

        Logger.Debug("{0} Mutex will not die, give up", account.Name);
        p.Kill(true);
        return false;
    }

    private bool KillMutex()
    {
        if (p == null) return true;

        var handle = Win32Handles.GetHandle(p.Id, MutexName, Win32Handles.MatchMode.EndsWith);

        if (handle == null) return p.MainWindowHandle != IntPtr.Zero;

        //Logger.Debug("{0} Got handle to Mutex", account.Name);
        handle.Kill();
        Logger.Debug("{0} Killed Mutex", account.Name);
        return true;
    }

    public async Task<bool> WaitForExit(LaunchType launchType, CancellationToken cancellationToken)
    {
        if (p == null)
        {
            Logger.Error("{0} No process", account.Name);
            RunStatus = RunState.Error;
            account.Client.StatusMessage = "No Process";
            return false;
        }

        if (launchType is not LaunchType.Update)
        {
            if (!await WaitForCharacterSelection(cancellationToken))
            {
                RunStatus = RunState.Cancelled;
                return false;
            }

            if (!EnterWorld())
            {
                return false;
            }
            if (launchType is LaunchType.Login) return await WaitForEntryThenExit(cancellationToken);
        }

        return await WaitForProcessToExit(cancellationToken);
    }

    private async Task<bool> WaitForProcessToExit(CancellationToken cancellationToken)
    {
        if (p == null)
        {
            Logger.Error("{0} No process", account.Name);
            return false;
        }

        // TODO add timeout?
        do
        {
            await Task.Delay(200, cancellationToken);
        } while (Alive);

        Logger.Info("{0} GW2 Closed", account.Name);
        return true;
    }

    private async Task<bool> WaitForEntryThenExit(CancellationToken cancellationToken)
    {
        if (!Alive) return false;
        Logger.Debug("{0} Wait for {1} to load-in to world", account.Name, account.Character ?? "character");
        if (!await WaitForStable(2000, 900_000, 2000, 180, cancellationToken))
        {
            Logger.Error("{0} Timed-out waiting for {1} to load into world", account.Name, account.Character ?? "character");
            return false;
        }

        if (!Alive)
        {
            Logger.Error("{0} Died!", account.Name);
            return false;
        }

        Logger.Debug("{0} {1} loaded into world OK, kill process", account.Name, account.Character ?? "character");
        LogManager.Flush();
        await Kill();
        return true;
    }

    private bool EnterWorld()
    {
        if (!Alive)
        {
            Logger.Error("{0} Died!", account.Name);
            return false;
        }

        Logger.Debug("{0} got to Character Selection", account.Name);
        LogManager.Flush();
        SendEnter(p);
        return true;
    }

    private async Task<bool> WaitForCharacterSelection(CancellationToken cancellationToken)
    {
        if (!Alive)
        {
            Logger.Error("{0} Died!", account.Name);
            return false;
        }

        lastMemoryUsage = p!.WorkingSet64 / 1024;

        Logger.Debug("{0} Wait for Character Selection", account.Name);
        if (await WaitForStable(2000, 750_000, 200, 120, cancellationToken)) return true;

        Logger.Error("{0} Timed-out waiting for Character Selection", account.Name);
        return false;
    }

    private async Task<bool> WaitForStable(int pause, long characterSelectMinMemory, long characterSelectMinDiff, double timeout, CancellationToken cancellationToken)
    {
        var start = DateTime.Now;
        do
        {
            await Task.Delay(pause, cancellationToken);
            if (MemoryUsageStable(characterSelectMinMemory, characterSelectMinDiff)) return true;
        } while (DateTime.Now.Subtract(start).TotalSeconds < timeout) ;
        return false;
    }

    private bool MemoryUsageStable(long min, long delta)
    {
        if (!Alive) return true;

        //Logger.Debug("{0} Window Title {1}", account.Name, p.MainWindowTitle);
        //Logger.Debug("{0} HandleCount {1}", account.Name, p.HandleCount);
        //Logger.Debug("{0} ThreadCount {1}", account.Name, p.Threads.Count);

        var memoryUsage = p.WorkingSet64 / 1024;
        var diff = Math.Abs(memoryUsage - lastMemoryUsage);
        lastMemoryUsage = memoryUsage;
        //Logger.Debug("{0} Mem={1} Diff={2}", account.Name, memoryUsage, diff);
        return memoryUsage > min && diff < delta;
    }

    private bool Alive
    {
        get
        {
            if (p == null) return false;
            p.Refresh();
            return !p.HasExited;
        }
    }

    private void SendEnter(Process? process)
    {
        if (process == null) return;

        Logger.Debug("{0} Send ENTER", account.Name);
        var currentFocus = Native.GetForegroundWindow();
        try
        {
            _ = Native.SetForegroundWindow(process.MainWindowHandle);
            InputSender.ClickKey(0x1c); // Enter
        }
        finally
        {
            _ = Native.SetForegroundWindow(currentFocus);
        }
    }

    public async Task<bool> Kill()
    {
        if (!Alive) return false;

        p!.Kill(true);
        await Task.Delay(200);
        return true;
    }

    private void Exited(object? sender, EventArgs e)
    {
        var deadProcess = sender as Process;
        Logger.Debug("{0} GW2 process exited", account.Name);
    }

}