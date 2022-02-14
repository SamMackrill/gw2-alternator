namespace guildwars2.tools.alternator.MVVM.model;


[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class Client : ObservableObject, IEquatable<Client>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string MutexName = "AN-Mutex-Window-Guild Wars 2";

    public IAccount Account { get; }

    private readonly List<string> loadedModules;

    private Process? p;
    private long lastStageMemoryUsage;
    private DateTime lastStageSwitchTime;
    private bool closed;
    private record struct EngineTuning(TimeSpan Pause, long MemoryUsage, long MinDiff, TimeSpan StuckDelay, int StuckTolerance);

    private EngineTuning tuning;

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

    public event EventHandler<ClientStateChangedEventArgs>? RunStatusChanged;

    private string? stuckReason;

    private string DebugDisplay => $"{Account.Name} Status:{RunStatus} Stage:{RunStage} {stuckReason}";

    private DateTime startAt;
    public DateTime StartAt
    {
        get => startAt;
        private set => SetProperty(ref startAt, value);
    }

    private DateTime authenticationAt;
    public DateTime AuthenticationAt
    {
        get => authenticationAt;
        private set => SetProperty(ref authenticationAt, value);
    }

    private DateTime loginAt;
    public DateTime LoginAt
    {
        get => loginAt;
        private set => SetProperty(ref loginAt, value);
    }

    private DateTime enterAt;
    public DateTime EnterAt
    {
        get => enterAt;
        private set => SetProperty(ref enterAt, value);
    }

    private DateTime exitAt;
    public DateTime ExitAt
    {
        get => exitAt;
        private set => SetProperty(ref exitAt, value);
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

    public string? StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public Client(IAccount account)
    {
        Account = account;
        RunStatus = RunState.Ready;
        loadedModules = new List<string>();
        RunStage = RunStage.NotRun;
    }

    public async Task Launch(
        LaunchType launchType,
        Settings settings,
        DirectoryInfo applicationFolder, 
        CancellationToken cancellationToken)
    {

        async Task CheckIfMovedOn(long memoryUsage)
        {
            var stageDiff = Math.Abs(memoryUsage - lastStageMemoryUsage);
            if (RunStage == RunStage.CharacterSelectReached && stageDiff > 100)
            {
                await ChangeRunStage(RunStage.CharacterSelected, 200, "Memory increased", cancellationToken);
            }
        }

        async Task CheckMemoryThresholdReached(long diff, long memoryUsage)
        {
            if (RunStage is not (RunStage.Authenticated or RunStage.CharacterSelected)) return;

            if (diff >= tuning.MinDiff) return;

            //Logger.Debug("{0} Memory={1} ({2}<{3})", Account.Name, memoryUsage, diff, tuning.MinDiff);
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

        async Task CheckIfStuck(long memoryUsage, long diff)
        {
            if (RunStage is not (RunStage.Authenticated or RunStage.ReadyToPlay)) return;

            if (diff >= tuning.StuckTolerance) return;

            var switchTime = DateTime.Now.Subtract(lastStageSwitchTime);
            var staticTooLong = switchTime > tuning.StuckDelay;
            if (!staticTooLong && !ErrorDetected(settings.ExperimentalErrorDetection)) return;

            stuckReason = staticTooLong ? $"Stuck, took too long ({switchTime.TotalSeconds:F1}s>{tuning.StuckDelay.TotalSeconds}s)" : "Login Error Detected";
            switch (RunStage)
            {
                case RunStage.ReadyToPlay:
                case RunStage.Authenticated:
                    Logger.Debug("{0} Stuck awaiting login, mem={1} diff={2} (because: {3})", Account.Name, memoryUsage, diff, stuckReason);
                    //CaptureWindow(RunStage.EntryFailed, applicationFolder);
                    await ChangeRunStage(RunStage.LoginFailed, 20, stuckReason, cancellationToken);
                    break;
            }
            
        }

        bool DetectCrash() => !closed && launchType != LaunchType.Collect && launchType != LaunchType.Update;

        Dictionary<string, RunStage> runStageFromModules = new()
        {
            { @"winnsi.dll", RunStage.Authenticated },
            { @"userenv.dll", RunStage.CharacterSelectReached },
            { @"mmdevapi.dll", RunStage.Playing },
        };


        async Task CheckIfStageUpdated()
        {
            if (launchType == LaunchType.Update) return;

            var memoryUsage = p.WorkingSet64 / 1024;

            await CheckIfMovedOn(memoryUsage);

            var diff = Math.Abs(memoryUsage - tuning.MemoryUsage);
            await CheckIfStuck(memoryUsage, diff);

            await CheckMemoryThresholdReached(diff, memoryUsage);

            tuning.MemoryUsage = memoryUsage;

            var newModules = UpdateProcessModules();
            var stageChanges = runStageFromModules.Where(e => newModules.Contains(e.Key)).OrderBy(e => e.Key);
            foreach (var (key, value) in stageChanges)
            {
                await ChangeRunStage(value, 200, $"Module {key} loaded", cancellationToken);
            }
        }

        tuning = new EngineTuning(new TimeSpan(0, 0, 0, 0, 200), 0L, 1L, new TimeSpan(0, 0, settings.StuckTimeout), 100);

        // Run gw2 exe with arguments
        var gw2Arguments = launchType is LaunchType.Update ? "-image" : $"-windowed -nosound -shareArchive -maploadinfo -dx9 -fps 20 -autologin"; // -dat \"{account.LoginFile}\""
        p = new Process
        {
            StartInfo = new ProcessStartInfo(Path.Combine(settings.Gw2Folder!, "Gw2-64.exe"))
            {
                CreateNoWindow = true,
                Arguments = gw2Arguments,
                UseShellExecute = false,
                WorkingDirectory = settings.Gw2Folder,
            }
        };
        p.Exited += Gw2Exited;

        loadedModules.Clear();

        await Start(launchType, cancellationToken);

        if (launchType is not LaunchType.Update) KillMutex();

        var timeout = launchType == LaunchType.Collect ? TimeSpan.MaxValue : new TimeSpan(0, 5, 0);
        // State Engine
        while (Alive)
        {
            if (DateTime.Now.Subtract(StartAt) > timeout)
            {
                Logger.Debug("{0} Timed-out after {1}s, giving up)", Account.Name, timeout.TotalSeconds);
                await Shutdown();
                throw new Gw2Exception("GW2 process timed-out");
            }

            await CheckIfStageUpdated();

            await Task.Delay(tuning.Pause, cancellationToken);
        }
        if (!string.IsNullOrEmpty(stuckReason)) throw new Gw2Exception($"GW2 process stuck: {stuckReason}");
        if (DetectCrash()) throw new Gw2Exception("GW2 process crashed");
    }


    private bool ErrorDetected(ErrorDetection errorDetection)
    {
        if (!Alive || errorDetection!=ErrorDetection.DelayAndPixel) return false;

        try
        {
            // TODO Add error state detection
        }
        catch (Exception e)
        {
            Logger.Error(e, "{0} ErrorDetected failed", Account.Name);
        }

        return false;
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

    private async Task ChangeRunStage(RunStage newRunStage, int delay, string? reason, CancellationToken cancellationToken)
    {
        if (delay>0) await Task.Delay(delay, cancellationToken);
        ChangeRunStage(newRunStage, reason);
    }

    private void ChangeRunStage(RunStage newRunStage, string? reason)
    {
        Logger.Debug("{0} Change State to {1} because {2}", Account.Name, newRunStage, reason);
        switch (newRunStage)
        {
            case RunStage.Authenticated:
                AuthenticationAt = DateTime.Now;
                break;
            case RunStage.ReadyToPlay:
                LoginAt = DateTime.Now;
                break;
            case RunStage.CharacterSelected:
                EnterAt = DateTime.Now;
                break;
        }

        var eventArgs = new ClientStateChangedEventArgs(RunStage, newRunStage);
        RunStage = newRunStage;
        if (!p!.HasExited) lastStageMemoryUsage = p!.WorkingSet64 / 1024;
        lastStageSwitchTime = DateTime.Now;
        UpdateEngineSpeed();
        RunStatusChanged?.Invoke(this, eventArgs);
    }

    private async Task Start(LaunchType launchType, CancellationToken cancellationToken)
    {
        if (!p!.Start()) throw new Gw2Exception($"{Account.Name} Failed to start");

        RunStatus = RunState.Running;
        StartAt = p.StartTime;
        Logger.Debug("{0} Started {1}", Account.Name, launchType);
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
            Logger.Error("{0} Mutex will not die, give up", Account.Name);
            p.Kill(true);
            throw new Gw2Exception($"{Account.Name} Mutex will not die, give up");
        }

        //Logger.Debug("{0} Got handle to Mutex", account.Name);
        handle.Kill();
        Logger.Debug("{0} Killed Mutex", Account.Name);
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

        Logger.Debug("{0} Send ENTER", Account.Name);
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

    public void MinimiseWindow()
    {
        _ = Native.ShowWindowAsync(p!.MainWindowHandle, ShowWindowCommands.ForceMinimize);
    }

    public void RestoreWindow()
    {
        _ = Native.ShowWindowAsync(p!.MainWindowHandle, ShowWindowCommands.Restore);
    }

    public async Task<bool> Shutdown()
    {
        closed = true;
        return await Kill();
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
        ChangeRunStage(RunStage.Exited, "Process.Exit event");
        Logger.Debug("{0} GW2 process exited", Account.Name);
        ExitAt = DateTime.Now;
    }

    public bool Equals(Client? other) => Account.Equals(other?.Account);
}

