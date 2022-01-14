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
    private long lastStageMemoryUsage;
    private DateTime lastStageSwitchTime;
    private bool closed;
    private record struct EngineTuning(TimeSpan Pause, long MemoryUsage, long MinDiff);

    private EngineTuning tuning;

    public event EventHandler<ClientStateChangedEventArgs>? RunStatusChanged;

    public int Attempt
    {
        get => attempt;
        set => SetProperty(ref attempt, value);
    }

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
        private set
        {
            if (SetProperty(ref runStage, value) && runStatus != RunState.Error) StatusMessage = $"Stage: {runStage}";
        }
    }

    private string? statusMessage;
    private int attempt;

    public string? StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public Client(Account account)
    {
        this.account = account;
        RunStatus = RunState.Ready;
        loadedModules = new List<string>();
        RunStage = RunStage.NotRun;
        Attempt = 0;
    }

    public async Task Launch(LaunchType launchType, string gw2Location, DirectoryInfo applicationFolder, CancellationToken cancellationToken)
    {
        bool DetectCrash() => !closed && launchType != LaunchType.Collect && launchType != LaunchType.Update;

        Dictionary<string, RunStage> runStageFromModules = new()
        {
            { @"winnsi.dll", RunStage.Authenticated },
            { @"userenv.dll", RunStage.CharacterSelectReached },
            { @"mmdevapi.dll", RunStage.Playing },
        };

        var stuckDelay = new TimeSpan(0, 0, 20);
        var stuckTolerance = 100;

        async Task StageUpdate()
        {
            if (launchType == LaunchType.Update) return;

            var memoryUsage = p.WorkingSet64 / 1024;

            // Check if Stuck
            var stuckDiff = Math.Abs(memoryUsage - lastStageMemoryUsage);
            if (stuckDiff < stuckTolerance && DateTime.Now.Subtract(lastStageSwitchTime) > stuckDelay)
            {
                switch (RunStage)
                {
                    case RunStage.ReadyToPlay:
                        Logger.Debug("{0} Stuck awaiting login, diff={1} after {2}s)", account.Name, stuckDiff, stuckDelay.TotalSeconds);
                        CaptureWindow(RunStage.EntryFailed, applicationFolder);
                        await ChangeRunStage(RunStage.EntryFailed, 200, "Stuck", cancellationToken);
                        break;
                    case RunStage.CharacterSelected:
                        Logger.Debug("{0} Stuck awaiting entry, diff={1} after {2}s)", account.Name, stuckDiff, stuckDelay.TotalSeconds);
                        CaptureWindow(RunStage.EntryFailed, applicationFolder);
                        await ChangeRunStage(RunStage.EntryFailed, 200, "Stuck", cancellationToken);
                        break;
                }
            }

            // Check if moved on
            var stageDiff = Math.Abs(memoryUsage - lastStageMemoryUsage);
            if (RunStage == RunStage.CharacterSelectReached && stageDiff > 100)
            {
                await ChangeRunStage(RunStage.CharacterSelected, 200, "Memory increased", cancellationToken);
            }

            var diff = Math.Abs(memoryUsage - tuning.MemoryUsage);
            if (RunStage is RunStage.Authenticated or RunStage.CharacterSelected && diff < tuning.MinDiff)
            {
                Logger.Debug("{0} Memory={1} ({2}<{3})", account.Name, memoryUsage, diff, tuning.MinDiff);
                switch (RunStage)
                {
                    case RunStage.Authenticated when memoryUsage > 120_000:
                        await ChangeRunStage(RunStage.ReadyToPlay, 4000, "Memory threshold", cancellationToken);
                        break;
                    case RunStage.CharacterSelected when memoryUsage > 1_400_000:
                        await ChangeRunStage(RunStage.WorldEntered, 1800, "Memory threshold", cancellationToken);
                        break;
                }
            }

            tuning.MemoryUsage = memoryUsage;

            var newModules = UpdateProcessModules();
            var stageChanges = runStageFromModules.Where(e => newModules.Contains(e.Key))
                .OrderBy(e => e.Key);
            foreach (var (key, value) in stageChanges)
            {
                await ChangeRunStage(value, 200, $"Module {key} loaded", cancellationToken);
            }
        }

        Attempt++;

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

        await Start(launchType, cancellationToken);

        if (launchType is not LaunchType.Update) KillMutex();

        var timeout = launchType == LaunchType.Collect ? TimeSpan.MaxValue : new TimeSpan(0, 5, 0);
        // State Engine
        while (Alive)
        {
            if (DateTime.Now.Subtract(StartTime) > timeout)
            {
                Logger.Debug("{0} Timed-out after {1}s, giving up)", account.Name, timeout.TotalSeconds);
                await Kill(true);
                throw new Gw2Exception("GW2 process timed-out");
            }

            await StageUpdate();

            await Task.Delay(tuning.Pause, cancellationToken);
        }
        if (DetectCrash()) throw new Gw2Exception("GW2 process crashed");
    }

    private void CaptureWindow(RunStage stage, DirectoryInfo applicationFolder)
    {
        if (!Alive) return;

        try
        {
            var shot = Native.PrintWindow(p!.MainWindowHandle);
            if (shot == null) throw new Exception("PrintWindow failed");
            var shotFile = Path.Combine(applicationFolder.FullName, $"shot_{stage}_{Guid.NewGuid()}.png");
            shot.Save(shotFile, ImageFormat.Png);

            var pixel = shot.GetPixel(10, 10);
        }
        catch (Exception e)
        {
            Logger.Error(e, "{0} CaptureWindow failed", account.Name);
        }
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

    private async Task ChangeRunStage(RunStage newRunStage, int delay, string reason, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        ChangeRunStage(newRunStage, reason);
    }

    private void ChangeRunStage(RunStage newRunStage, string reason)
    {
        Logger.Debug("{0} Change State to {1} because {2}", account.Name, newRunStage, reason);
        var eventArgs = new ClientStateChangedEventArgs(RunStage, newRunStage);
        RunStage = newRunStage;
        if (!p!.HasExited) lastStageMemoryUsage = p!.WorkingSet64 / 1024;
        lastStageSwitchTime = DateTime.Now;
        UpdateEngineSpeed();
        RunStatusChanged?.Invoke(this, eventArgs);
    }

    private async Task Start(LaunchType launchType, CancellationToken cancellationToken)
    {
        if (!p!.Start()) throw new Gw2Exception($"{account.Name} Failed to start");
        
        RunStatus = RunState.Running;
        StartTime = p.StartTime;
        Logger.Debug("{0} Started {1}", account.Name, launchType);
        await ChangeRunStage(RunStage.Started, 200, "Normal start", cancellationToken);
    }

    public void SelectCharacter()
    {
        SendEnter();
    }

    private void KillMutex()
    {
        if (p == null) return;
        p.WaitForInputIdle();

        var handle = Win32Handles.GetHandle(p.Id, MutexName, Win32Handles.MatchMode.EndsWith);

        if (handle == null)
        {
            if (p.MainWindowHandle != IntPtr.Zero) return;
            Logger.Error("{0} Mutex will not die, give up", account.Name);
            p.Kill(true);
            throw new Gw2Exception($"{account.Name} Mutex will not die, give up");
        }

        //Logger.Debug("{0} Got handle to Mutex", account.Name);
        handle.Kill();
        Logger.Debug("{0} Killed Mutex", account.Name);
    }

    private List<string> UpdateProcessModules()
    {
        var newModules = new List<string>();
        if (!Alive) return newModules;

        foreach (ProcessModule module in p!.Modules)
        {
            var moduleName = module?.ModuleName?.ToLowerInvariant();
            if (moduleName == null || loadedModules.Contains(moduleName)) continue;
            //Logger.Debug("{0} Module: {1}", account.Name, moduleName);
            loadedModules.Add(moduleName);
            newModules.Add(moduleName);
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
        if (!Alive) return;

        Logger.Debug("{0} Send ENTER", account.Name);
        var currentFocus = Native.GetForegroundWindow();
        try
        {
            _ = Native.SetForegroundWindow(p!.MainWindowHandle);
            InputSender.ClickKey(0x1c); // Enter
        }
        finally
        {
            _ = Native.SetForegroundWindow(currentFocus);
        }
    }

    public async Task<bool> Kill(bool done)
    {
        closed = done;
        if (!Alive) return false;

        p!.Kill(true);
        await Task.Delay(200);
        return true;
    }

    private void Gw2Exited(object? sender, EventArgs e)
    {
        ChangeRunStage(RunStage.Exited, "Process.Exit event");
        Logger.Debug("{0} GW2 process exited", account.Name);
    }

}

