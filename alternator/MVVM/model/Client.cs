namespace guildwars2.tools.alternator.MVVM.model;

public class Client : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string MutexName = "AN-Mutex-Window-Guild Wars 2";

    private readonly Account account;
    private Process? p;
    private List<string> loadedModules;

    public event EventHandler Started;
    public event EventHandler MutexRemoved;
    public event EventHandler Authenticated;
    public event EventHandler AuthenticationBlocked;
    public event EventHandler ReadyToPlay;
    public event EventHandler ReadyToSelectCharactor;
    public event EventHandler EnteredWorld;
    public event EventHandler Exited;


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

    private RunStage runStage;
    public RunStage RunStage
    {
        get => runStage;
        set
        {
            SetProperty(ref runStage, value);
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
        loadedModules = new();
        RunStage = RunStage.NotRun;
    }

    internal record struct ClientHandler(RunStage RunStage, EventHandler? Handler) { }
    private Dictionary<string, ClientHandler> eventsFromModules;

    public async Task Launch(LaunchType launchType, string gw2Location, CancellationToken cancellationToken)
    {
        eventsFromModules = new()
        {
            { "dpapi.dll", new ClientHandler(RunStage.Authenticated, Authenticated) },
            { "userenv.dll", new ClientHandler(RunStage.CharacterSelectReached, ReadyToSelectCharactor) },
        };

        // Run gw2 exe with arguments
        var gw2Arguments = launchType is LaunchType.Update ? "-image" : $"-windowed -nosound -shareArchive -maploadinfo -dx9 -fps 20 -autologin"; // -dat \"{account.LoginFile}\""
        var pi = new ProcessStartInfo(Path.Combine(gw2Location, "Gw2-64.exe"))
        {
            CreateNoWindow = true,
            Arguments = gw2Arguments,
            UseShellExecute = false,
            WorkingDirectory = gw2Location,
        };
        p = new Process { StartInfo = pi };
        p.Exited += Gw2Exited;

        loadedModules.Clear();

        Start(launchType);

        if (launchType is not LaunchType.Update) KillMutex();

        var timeout = new TimeSpan(0, 5, 0);
        var pause = new TimeSpan(0, 0, 0, 0, 100);
        var lastMemoryUsage = 0L;
        var minDiff = 1L;
        while (Alive && DateTime.Now.Subtract(StartTime) < timeout)
        {
            var memoryUsage = p.WorkingSet64 / 1024;
            var diff = Math.Abs(memoryUsage - lastMemoryUsage);
            if ((RunStage == RunStage.Authenticated || RunStage == RunStage.CharacterSelectReached) && diff < minDiff)
            {
                Logger.Debug("{0} Memory={1} ({2}<{3})", account.Name, memoryUsage, diff, minDiff);
                if (RunStage == RunStage.Authenticated && memoryUsage > 120_000)
                {
                    await Task.Delay(1800, cancellationToken);
                    Logger.Debug("{0} Ready to Play", account.Name, account.Character);
                    RunStage = RunStage.ReadyToPlay;
                    ReadyToPlay?.Invoke(this, EventArgs.Empty);
                }
                //else if ((RunStage == RunStage.ReadyToPlay || RunStage == RunStage.Authenticated) && memoryUsage > 750_000)
                //{
                //    if (RunStage == RunStage.Authenticated)
                //    {
                //        Logger.Debug("{0} Play already initiated", account.Name);
                //    }
                //    Logger.Debug("{0} Ready to Play", account.Name, account.Character);
                //    runStage = RunStage.CharacterSelectReached;
                //    ReadyToSelectCharactor?.Invoke(this, EventArgs.Empty);
                //}
                else if (RunStage == RunStage.CharacterSelectReached && memoryUsage > 1_400_000)
                {
                    await Task.Delay(1800, cancellationToken);
                    Logger.Debug("{0} World Entered {1}", account.Name, account.Character);
                    RunStage = RunStage.WorldEntered;
                    //Suspend();
                    EnteredWorld?.Invoke(this, EventArgs.Empty);
                    //Resume();
                }
            }
            lastMemoryUsage = memoryUsage;

            var newModules = UpdateProcessModules();
            var events = eventsFromModules.Where(e => newModules.Contains(e.Key)).Select(e => e.Value).ToList();
            foreach (var launchEvent in events)
            {
                Logger.Debug("{0} {1}", account.Name, launchEvent.RunStage);
                RunStage = launchEvent.RunStage;
                if (RunStage == RunStage.CharacterSelectReached)
                {
                    pause = new TimeSpan(0, 0, 0, 2, 0);
                    minDiff = 200;
                }
                launchEvent.Handler?.Invoke(this, EventArgs.Empty);
            }
            await Task.Delay(pause, cancellationToken);
        }
        if (launchType!=LaunchType.Collect) await Kill();
    }

    private void Start(LaunchType launchType)
    {
        _ = p.Start();
        RunStatus = RunState.Running;
        RunStage = RunStage.Started;
        StartTime = p.StartTime;
        Logger.Debug("{0} Started {1}", account.Name, launchType);
        Started?.Invoke(this, EventArgs.Empty);
    }

    public void Suspend()
    {
        if (!Alive) return;

        foreach (ProcessThread pT in p.Threads)
        {
            IntPtr pOpenThread = Native.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

            if (pOpenThread == IntPtr.Zero)  continue;

            Native.SuspendThread(pOpenThread);

            Native.CloseHandle(pOpenThread);
        }
    }

    public void Resume()
    {
        if (!Alive) return;
        foreach (ProcessThread pT in p.Threads)
        {
            IntPtr pOpenThread = Native.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

            if (pOpenThread == IntPtr.Zero)
            {
                continue;
            }

            uint suspendCount = 0;
            do
            {
                suspendCount = Native.ResumeThread(pOpenThread);
            } while (suspendCount > 0);

            Native.CloseHandle(pOpenThread);
        }
    }

    private void KillMutex()
    {
        if (p == null) return;
        p.WaitForInputIdle();

        var handle = Win32Handles.GetHandle(p.Id, MutexName, Win32Handles.MatchMode.EndsWith);

        if (handle == null)
        {
            if (p.MainWindowHandle == IntPtr.Zero)
            {
                Logger.Error("{0} Mutex will not die, give up", account.Name);
                p.Kill(true);
                throw new Gw2Exception($"{account.Name} Mutex will not die, give up");
            }
            return;
        }

        //Logger.Debug("{0} Got handle to Mutex", account.Name);
        handle.Kill();
        Logger.Debug("{0} Killed Mutex", account.Name);
        MutexRemoved?.Invoke(this, EventArgs.Empty);
    }

    private List<string> UpdateProcessModules()
    {
        var newModules = new List<string>();
        if (Alive)
        {
            foreach (ProcessModule module in p.Modules)
            {
                var moduleName = module?.ModuleName?.ToLowerInvariant();
                if (moduleName == null || loadedModules.Contains(moduleName)) continue;
                Logger.Debug("{0} Module: {1}", account.Name, moduleName);
                loadedModules.Add(moduleName);
                newModules.Add(moduleName);
            }
        }

        return newModules;
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

    public void SendEnter()
    {
        if (p == null) return;

        Logger.Debug("{0} Send ENTER", account.Name);
        var currentFocus = Native.GetForegroundWindow();
        try
        {
            _ = Native.SetForegroundWindow(p.MainWindowHandle);
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

    private void Gw2Exited(object? sender, EventArgs e)
    {
        var deadProcess = sender as Process;
        Logger.Debug("{0} GW2 process exited", account.Name);
        Exited?.Invoke(this, EventArgs.Empty);
    }

    //public async Task<bool> WaitForExit(LaunchType launchType, CancellationToken cancellationToken)
    //{
    //    if (p == null)
    //    {
    //        Logger.Error("{0} No process", account.Name);
    //        RunStatus = RunState.Error;
    //        account.Client.StatusMessage = "No Process";
    //        return false;
    //    }

    //    if (launchType is not LaunchType.Update)
    //    {
    //        if (!await WaitForCharacterSelection(cancellationToken))
    //        {
    //            RunStatus = RunState.Cancelled;
    //            return false;
    //        }

    //        if (!EnterWorld())
    //        {
    //            return false;
    //        }
    //        if (launchType is LaunchType.Login) return await WaitForEntryThenExit(cancellationToken);
    //    }

    //    return await WaitForProcessToExit(cancellationToken);
    //}

    //private async Task<bool> WaitForProcessToExit(CancellationToken cancellationToken)
    //{
    //    if (p == null)
    //    {
    //        Logger.Error("{0} No process", account.Name);
    //        return false;
    //    }

    //    // TODO add timeout?
    //    do
    //    {
    //        await Task.Delay(200, cancellationToken);
    //    } while (Alive);

    //    Logger.Info("{0} GW2 Closed", account.Name);
    //    return true;
    //}

    //private async Task<bool> WaitForEntryThenExit(CancellationToken cancellationToken)
    //{
    //    if (!Alive) return false;
    //    Logger.Debug("{0} Wait for {1} to load-in to world", account.Name, account.Character ?? "character");
    //    if (!await WaitForStable(2000, 900_000, 2000, 180, cancellationToken))
    //    {
    //        Logger.Error("{0} Timed-out waiting for {1} to load into world", account.Name, account.Character ?? "character");
    //        return false;
    //    }

    //    if (!Alive)
    //    {
    //        Logger.Error("{0} Died!", account.Name);
    //        return false;
    //    }

    //    Logger.Debug("{0} {1} loaded into world OK, kill process", account.Name, account.Character ?? "character");
    //    LogManager.Flush();
    //    await Kill();
    //    return true;
    //}

    //private bool EnterWorld()
    //{
    //    if (!Alive)
    //    {
    //        Logger.Error("{0} Died!", account.Name);
    //        return false;
    //    }

    //    Logger.Debug("{0} got to Character Selection", account.Name);
    //    LogManager.Flush();
    //    SendEnter(p);
    //    return true;
    //}

    //private async Task<bool> WaitForCharacterSelection(CancellationToken cancellationToken)
    //{
    //    if (!Alive)
    //    {
    //        Logger.Error("{0} Died!", account.Name);
    //        return false;
    //    }

    //    lastMemoryUsage = p!.WorkingSet64 / 1024;

    //    Logger.Debug("{0} Wait for Character Selection", account.Name);
    //    if (await WaitForStable(200, 750_000, 200, 120, cancellationToken)) return true;

    //    Logger.Error("{0} Timed-out waiting for Character Selection", account.Name);
    //    return false;
    //}

    //private async Task<bool> WaitForStable(int pause, long characterSelectMinMemory, long characterSelectMinDiff, double timeout, CancellationToken cancellationToken)
    //{
    //    var start = DateTime.Now;
    //    do
    //    {
    //        UpdateProcessModules();
    //        await Task.Delay(pause, cancellationToken);
    //        if (MemoryUsageStable(characterSelectMinMemory, characterSelectMinDiff)) return true;
    //    } while (DateTime.Now.Subtract(start).TotalSeconds < timeout) ;
    //    return false;
    //}


    //private bool MemoryUsageStable(long min, long delta)
    //{
    //    if (!Alive) return true;

    //    //Logger.Debug("{0} Window Title {1}", account.Name, p.MainWindowTitle);
    //    //Logger.Debug("{0} HandleCount {1}", account.Name, p.HandleCount);
    //    //Logger.Debug("{0} ThreadCount {1}", account.Name, p.Threads.Count);

    //    var memoryUsage = p.WorkingSet64 / 1024;
    //    var diff = Math.Abs(memoryUsage - lastMemoryUsage);
    //    lastMemoryUsage = memoryUsage;
    //    //Logger.Debug("{0} Mem={1} Diff={2}", account.Name, memoryUsage, diff);
    //    return memoryUsage > min && diff < delta;
    //}


}

