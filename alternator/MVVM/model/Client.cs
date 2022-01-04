namespace guildwars2.tools.alternator.MVVM.model;

public class ClientStateChangedEventArgs : EventArgs
{
    public ClientStateChangedEventArgs(RunStage oldState, RunStage state)
    {
        OldState = oldState;
        State = state;
    }

    public RunStage OldState { get; }
    public RunStage State { get; }
}

public class Client : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string MutexName = "AN-Mutex-Window-Guild Wars 2";

    private readonly Account account;
    private readonly List<string> loadedModules;

    private Process? p;
    private long characterSelectMemoryUsage;
    private bool closed;
    private record struct EngineTuning(TimeSpan Pause, long MemoryUsage, long MinDiff);

    private EngineTuning tuning;

    public event EventHandler<ClientStateChangedEventArgs>? RunStatusChanged;


    private DateTime startTime;
    public DateTime StartTime
    {
        get => startTime;
        private set => SetProperty(ref startTime, value);
    }

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
        private set => SetProperty(ref runStage, value);
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

    public async Task Launch(LaunchType launchType, string gw2Location, CancellationToken cancellationToken)
    {

        Dictionary<string, RunStage> runStageFromModules = new()
        {
            { @"winnsi.dll", RunStage.Authenticated },
            { @"userenv.dll", RunStage.CharacterSelectReached },
            { @"mmdevapi.dll", RunStage.Playing },
        };

        tuning = new EngineTuning(new TimeSpan(0, 0, 0, 0, 200), 0L, 1L);

        // Run gw2 exe with arguments
        var gw2Arguments = launchType is LaunchType.Update ? "-image" : $"-windowed -nosound -shareArchive -maploadinfo -dx9 -fps 20 -autologin"; // -dat \"{account.LoginFile}\""
        p = new Process { StartInfo = new ProcessStartInfo(Path.Combine(gw2Location, "Gw2-64.exe"))
        {
            CreateNoWindow = true,
            Arguments = gw2Arguments,
            UseShellExecute = false,
            WorkingDirectory = gw2Location,
        } };
        p.Exited += Gw2Exited;

        loadedModules.Clear();

        Start(launchType);

        if (launchType is not LaunchType.Update) KillMutex();

        // TODO add failed login detection
        var timeout = launchType == LaunchType.Collect ? TimeSpan.MaxValue : new TimeSpan(0, 5, 0);
        // State Engine
        while (Alive)
        {
            if (DateTime.Now.Subtract(StartTime) > timeout) throw new Gw2Exception("GW2 process timed-out");

            var memoryUsage = p.WorkingSet64 / 1024;

            if (RunStage == RunStage.CharacterSelectReached && memoryUsage - characterSelectMemoryUsage > 100)
            {
                await Task.Delay(200, cancellationToken);
                ChangeRunStage(RunStage.CharacterSelected);
            }

            var diff = Math.Abs(memoryUsage - tuning.MemoryUsage);
            if ((RunStage == RunStage.Authenticated || RunStage >= RunStage.CharacterSelected) && diff < tuning.MinDiff)
            {
                Logger.Debug("{0} Memory={1} ({2}<{3})", account.Name, memoryUsage, diff, tuning.MinDiff);
                if (RunStage == RunStage.Authenticated && memoryUsage > 120_000)
                {
                    await Task.Delay(1800, cancellationToken);
                    ChangeRunStage(RunStage.ReadyToPlay);
                }
                else if (RunStage >= RunStage.CharacterSelected && memoryUsage > 1_400_000)
                {
                    await Task.Delay(1800, cancellationToken);
                    //Suspend();
                    ChangeRunStage(RunStage.WorldEntered);
                    //Resume();
                }
            }
            tuning.MemoryUsage = memoryUsage;

            var newModules = UpdateProcessModules();
            var stageChanges = runStageFromModules.Where(e => newModules.Contains(e.Key))
                                                  .OrderBy(e => e.Key)
                                                  .Select(e => e.Value);
            foreach (var newRunStage in stageChanges)
            {
                ChangeRunStage(newRunStage);
            }
            await Task.Delay(tuning.Pause, cancellationToken);
        }
        if (!closed) throw new Gw2Exception("GW2 process crashed");
    }

    private void UpdateEngineSpeed()
    {
        switch (RunStage)
        {
            case >= RunStage.CharacterSelectReached:
                tuning.Pause = new TimeSpan(0, 0, 0, 2, 0);
                tuning.MinDiff = 200L;
                break;
        }
    }

    private void ChangeRunStage(RunStage newRunStage)
    {
        Logger.Debug("{0} Change State to {1}", account.Name, newRunStage);
        var eventArgs = new ClientStateChangedEventArgs(RunStage, newRunStage);
        RunStage = newRunStage;
        UpdateEngineSpeed();
        RunStatusChanged?.Invoke(this, eventArgs);
    }

    private void Start(LaunchType launchType)
    {
        if (!p!.Start()) throw new Gw2Exception($"{account.Name} Failed to start");
        
        RunStatus = RunState.Running;
        StartTime = p.StartTime;
        Logger.Debug("{0} Started {1}", account.Name, launchType);
        ChangeRunStage(RunStage.Started);
    }

    public void SelectCharacter()
    {
        characterSelectMemoryUsage = p!.WorkingSet64 / 1024;
        SendEnter();
    }

    //public void Suspend()
    //{
    //    if (!Alive) return;

    //    foreach (ProcessThread pT in p.Threads)
    //    {
    //        IntPtr pOpenThread = Native.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

    //        if (pOpenThread == IntPtr.Zero)  continue;

    //        Native.SuspendThread(pOpenThread);

    //        Native.CloseHandle(pOpenThread);
    //    }
    //}

    //public void Resume()
    //{
    //    if (!Alive) return;
    //    foreach (ProcessThread pT in p.Threads)
    //    {
    //        IntPtr pOpenThread = Native.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

    //        if (pOpenThread == IntPtr.Zero)
    //        {
    //            continue;
    //        }

    //        uint suspendCount;
    //        do
    //        {
    //            suspendCount = Native.ResumeThread(pOpenThread);
    //        } while (suspendCount > 0);

    //        Native.CloseHandle(pOpenThread);
    //    }
    //}

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
    }

    private List<string> UpdateProcessModules()
    {
        var newModules = new List<string>();
        if (Alive)
        {
            foreach (ProcessModule module in p!.Modules)
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

    public async Task<bool> Kill(bool done)
    {
        if (!Alive) return false;

        p!.Kill(true);
        await Task.Delay(200);
        closed = done;
        return true;
    }

    private void Gw2Exited(object? sender, EventArgs e)
    {
        ChangeRunStage(RunStage.Exited);
        Logger.Debug("{0} GW2 process exited", account.Name);
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

