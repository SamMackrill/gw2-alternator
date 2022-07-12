namespace guildwars2.tools.alternator.MVVM.model;

[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class Client : ObservableObject, IEquatable<Client>
{
    private readonly ILogger? launchLogger;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string MutexName = "AN-Mutex-Window-Guild Wars 2";

    public IAccount Account { get; }
    public int AccountIndex { get; }

    private readonly List<string> loadedModules;

    private Process? p;
    private long lastStageMemoryUsage;
    private DateTime lastStageSwitchTime;
    private bool closed;
    private bool killed;
    private LaunchType launchType;
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

    private string? failedReason;

    private string DebugDisplay => $"{Account?.Name ?? "Unset"} Status:{RunStatus} Stage:{RunStage} {failedReason ?? ""}";

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
            if (!SetProperty(ref runStatus, value)) return;
            if (runStatus != RunState.Error) StatusMessage = null;
            AccountLogger?.Debug("RunStatus: {0})", runStatus);
        }
    }

    private RunStage runStage;
    public RunStage RunStage
    {
        get => runStage;
        private set
        {
            if (!SetProperty(ref runStage, value)) return;
            if (runStatus != RunState.Error) StatusMessage = $"Stage: {runStage}";
            AccountLogger?.Debug("RunStage: {0})", runStage);
        }
    }

    private ExitReason exitReason;
    public ExitReason ExitReason
    {
        get => exitReason;
        private set => SetProperty(ref exitReason, value);
    }

    private string? statusMessage;

    public string? StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public ILogger? AccountLogger { get; private set; }
    private LogFactory? logFactory;
    public void ClearLogging()
    {
        logFactory?.Shutdown();
    }

    public Client(IAccount account, int accountIndex, ILogger? launchLogger)
    {
        this.launchLogger = launchLogger;
        Account = account;
        AccountIndex = accountIndex;
        RunStatus = RunState.Ready;
        loadedModules = new List<string>();
        RunStage = RunStage.NotRun;
    }

    public async Task Launch(
        LaunchType launchType,
        Settings settings,
        bool shareArchive,
        bool accountLogs,
        DirectoryInfo applicationFolder, 
        CancellationToken cancellationToken
        )
    {
        this.launchType = launchType;
        if (accountLogs)
        {
            logFactory = new LogFactory();
            var fileTarget = new FileTarget("AccountLogger")
            {
                FileName = Path.Combine(applicationFolder.FullName, "AccountLogs", $"{Account.Name!.GetSafeFileName()}-{launchType}-log.txt"),
                Layout = new SimpleLayout { Text = "${longdate}|${message:withexception=true}" },
                ArchiveOldFileOnStartup = true,
                ArchiveNumbering = ArchiveNumberingMode.Sequence,
                MaxArchiveDays = 1,
            };
            var config = new LoggingConfiguration();
            config.AddTarget(fileTarget);
            config.AddRuleForAllLevels(fileTarget);
            logFactory.Configuration = config;

            AccountLogger = logFactory.GetCurrentClassLogger();
            Logger.Debug("{0} Logging to {1}", Account.Name, fileTarget.FileName);
        }

        async Task CheckIfMovedOn(long memoryUsage)
        {
            var stageDiff = Math.Abs(memoryUsage - lastStageMemoryUsage);
            if (RunStage == RunStage.CharacterSelectReached && stageDiff > settings.DeltaMemoryThreshold)
            {
                await ChangeRunStage(RunStage.CharacterSelected, 200, $"Memory increased by {stageDiff} > {settings.DeltaMemoryThreshold}", cancellationToken);
            }
        }

        async Task CheckMemoryThresholdReached(long diff, long memoryUsage)
        {
            if (RunStage is not (RunStage.Authenticated or RunStage.CharacterSelected)) return;

            if (diff >= tuning.MinDiff) return;

            //Logger.Debug("{0} Memory={1} ({2}<{3})", Account.Name, memoryUsage, diff, tuning.MinDiff);
            switch (RunStage)
            {
                case RunStage.Authenticated when memoryUsage > settings.AuthenticationMemoryThreshold * 1000:
                    await ChangeRunStage(RunStage.ReadyToPlay, 4000, $"Memory threshold {memoryUsage / 1000} > {settings.AuthenticationMemoryThreshold}", cancellationToken);
                    break;
                case RunStage.CharacterSelected when memoryUsage > settings.CharacterSelectedMemoryThreshold * 1000:
                    await ChangeRunStage(RunStage.WorldEntered, 1800, $"Memory threshold {memoryUsage / 1000} > {settings.CharacterSelectedMemoryThreshold}", cancellationToken);
                    break;
            }
        }

        async Task CheckIfStuck(long memoryUsage, long diff)
        {
            if (RunStage is not (RunStage.Authenticated or RunStage.ReadyToPlay or RunStage.Playing)) return;

            if (diff >= tuning.StuckTolerance) return; // still doing something

            var switchTime = DateTime.UtcNow.Subtract(lastStageSwitchTime);
            if (switchTime < tuning.StuckDelay) return;

            failedReason = $"Stuck, took too long ({switchTime.TotalSeconds:F1}s>{tuning.StuckDelay.TotalSeconds}s)";

            Logger.Debug("{0} failed awaiting login, mem={1} diff={2} (because: {3})", Account.Name, memoryUsage, diff, failedReason);
            AccountLogger?.Debug("Failed awaiting login, mem={1} diff={2} (because: {3})", Account.Name, memoryUsage, diff, failedReason);

            //CaptureWindow(RunStage.EntryFailed, applicationFolder);
            await ChangeRunStage(RunStage.LoginFailed, 20, failedReason, cancellationToken);
        }

        const string playingSuccessModule = @"lglcdapi.dll";
        async Task CheckIfCrashed()
        {
            if (RunStage is not (RunStage.Playing)) return;

            var switchTime = DateTime.UtcNow.Subtract(lastStageSwitchTime);
            if (switchTime.TotalSeconds < settings.CrashWaitDelay) return;

            if (loadedModules.Contains(playingSuccessModule)) return;

            failedReason = $"Crashed state detected ({playingSuccessModule} not loaded within {settings.CrashWaitDelay}s)";

            Logger.Debug("{0} failed awaiting login (because: {1})", Account.Name, failedReason);
            AccountLogger?.Debug("Failed awaiting login (because: {1})", Account.Name, failedReason);

            await ChangeRunStage(RunStage.LoginCrashed, 20, failedReason, cancellationToken);
        }

        Dictionary<RunStage, List<string>> runStageFromModules = new()
        {
            { RunStage.Authenticated,          new List<string> { @"winnsi.dll" } },
            { RunStage.CharacterSelectReached, new List<string> { @"mscms.dll", @"coloradapterclient.dll", @"icm32.dll" } },
            { RunStage.Playing,                new List<string> { @"mmdevapi.dll" } },
        };


        async Task CheckIfStageUpdated()
        {
            await CheckIfCrashed();

            if (launchType == LaunchType.Update) return;

            var memoryUsage = p.WorkingSet64 / 1024;

            await CheckIfMovedOn(memoryUsage);

            var diff = Math.Abs(memoryUsage - tuning.MemoryUsage);
            await CheckIfStuck(memoryUsage, diff);

            await CheckMemoryThresholdReached(diff, memoryUsage);

            tuning.MemoryUsage = memoryUsage;

            var newModules = UpdateProcessModules();
            foreach (var runStageModules in runStageFromModules)
            {
                var module = runStageModules.Value.FirstOrDefault(module => newModules.Contains(module));
                if (module == null) continue;
                await ChangeRunStage(runStageModules.Key, 200, $"Module {module} loaded", cancellationToken);
                return;
            }
        }

        tuning = new EngineTuning(new TimeSpan(0, 0, 0, 0, 200), 0L, 1L, new TimeSpan(0, 0, settings.StuckTimeout), 100);

        string FormArguments(bool share)
        {
            if (launchType is LaunchType.Update) return "-image";

            var args = "-windowed -nosound -maploadinfo -dx9 -fps 20 -autologin";
            if (share) args += " -shareArchive";
            return args;
        }

        // Run gw2 exe with arguments
        var gw2Arguments = FormArguments(shareArchive);
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

        if (launchType is not LaunchType.Update) KillMutex(200, 500);

        var timeout = launchType == LaunchType.Collect ? TimeSpan.MaxValue : new TimeSpan(0, 0, settings.LaunchTimeout);
        // State Engine
        while (Alive)
        {
            if (DateTime.UtcNow.Subtract(StartAt) > timeout)
            {
                Logger.Debug("{0} Timed-out after {1}s, giving up)", Account.Name, timeout.TotalSeconds);
                launchLogger?.Info("{0} Timed-out after {1}s, giving up)", Account.Name, timeout.TotalSeconds);
                AccountLogger?.Debug("Timed-out after {1}s, giving up)", Account.Name, timeout.TotalSeconds);
                await Shutdown(0);
                throw new Gw2TimeoutException("GW2 process timed-out");
            }

            await CheckIfStageUpdated();

            await Task.Delay(tuning.Pause, cancellationToken);
        }

        if (closed) return;
        if (!string.IsNullOrEmpty(failedReason)) throw new Gw2TimeoutException($"GW2 process stuck: {failedReason}");
        if (!closed && launchType == LaunchType.Login) throw new Gw2CrashedException("GW2 process crashed");
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
        AccountLogger?.Debug("Change State to {1} because {2}", Account.Name, newRunStage, reason);
        switch (newRunStage)
        {
            case RunStage.Authenticated:
                AuthenticationAt = DateTime.UtcNow;
                break;
            case RunStage.ReadyToPlay:
                LoginAt = DateTime.UtcNow;
                break;
            case RunStage.CharacterSelected:
                EnterAt = DateTime.UtcNow;
                break;
        }

        var eventArgs = new ClientStateChangedEventArgs(RunStage, newRunStage);
        RunStage = newRunStage;
        if (!p!.HasExited) lastStageMemoryUsage = p!.WorkingSet64 / 1024;
        lastStageSwitchTime = DateTime.UtcNow;
        UpdateEngineSpeed();
        RunStatusChanged?.Invoke(this, eventArgs);
    }

    private async Task Start(LaunchType launchType, CancellationToken cancellationToken)
    {
        if (!p!.Start()) throw new Gw2Exception($"{Account.Name} Failed to start");

        RunStatus = RunState.Running;
        StartAt = p.StartTime.ToUniversalTime();
        Logger.Debug("{0} Started {1}", Account.Name, launchType);
        launchLogger?.Info("{0} Started {1}", Account.Name, launchType);
        AccountLogger?.Debug("Started {1}", Account.Name, launchType);
        await ChangeRunStage(RunStage.Started, 200, "Normal start", cancellationToken);
    }

    public void SelectCharacter()
    {
        SendEnter();
    }

    private async void KillMutex(int delayBefore, int delayAfter)
    {
        if (p == null) return;

        try
        {
            p.WaitForInputIdle();

            await Task.Delay(delayBefore);
            var handle = Win32Handles.GetHandle(p.Id, MutexName, Win32Handles.MatchMode.EndsWith);

            if (handle == null)
            {
                if (p.MainWindowHandle != IntPtr.Zero) return;
                Logger.Error("{0} Mutex not found", Account.Name);
                launchLogger?.Info("{0} Mutex not found", Account.Name);
                AccountLogger?.Error("Mutex not found", Account.Name);
                p.Kill(true);
                throw new Gw2MutexException($"{Account.Name} Mutex not found");
            }

            //Logger.Debug("{0} Got handle to Mutex", account.Name);
            handle.Kill();
            Logger.Debug("{0} Killed Mutex", Account.Name);
            AccountLogger?.Debug("Killed Mutex", Account.Name);
            await Task.Delay(delayAfter);
        }
        catch (Exception e)
        {
            Logger.Error(e, "{0} error killing Mutex, ignoring", Account.Name);
            launchLogger?.Info("{0} error killing Mutex, ignoring", Account.Name);
            AccountLogger?.Error(e, "{0} error killing Mutex, ignoring", Account.Name);
        }
    }

    private List<string> UpdateProcessModules()
    {
        var newModules = new List<string>();
        if (!Alive) return newModules;

        foreach (ProcessModule module in p!.Modules)
        {
            var moduleName = module?.ModuleName?.ToLowerInvariant();
            if (moduleName == null || loadedModules.Contains(moduleName)) continue;
            AccountLogger?.Debug("Module: {1}", Account.Name, moduleName);
            loadedModules.Add(moduleName);
            newModules.Add(moduleName);
        }

        return newModules;
    }

    private bool Alive
    {
        get
        {
            if (p == null || RunStage == RunStage.NotRun) return false;
            try
            {
                p.Refresh();
                return !p.HasExited;

            }
            catch (Exception e)
            {
                Logger.Error(e, "{0} Error checking if process alive)", Account.Name);
                AccountLogger?.Debug(e, "Error checking if process alive)", Account.Name);
            }
            return false;
        }
    }

    public void SendEnter()
    {
        if (!Alive) return;

        Logger.Debug("{0} Send ENTER", Account.Name);
        AccountLogger?.Debug("Send ENTER", Account.Name);
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
        AccountLogger?.Debug("GW2 Hide", Account.Name);
        _ = Native.ShowWindowAsync(p!.MainWindowHandle, ShowWindowCommands.ForceMinimize);
    }

    public void RestoreWindow()
    {
        AccountLogger?.Debug("GW2 Show", Account.Name);
        _ = Native.ShowWindowAsync(p!.MainWindowHandle, ShowWindowCommands.Restore);
    }

    public async Task<bool> Shutdown(int delay)
    {
        Logger.Debug("{0} Shutdown requested", Account.Name);
        AccountLogger?.Debug("Shutdown requested", Account.Name);
        closed = true;
        await Task.Delay(delay);
        return await Kill();
    }

    public async Task<bool> Kill()
    {
        if (!Alive) return false;

        AccountLogger?.Debug("Kill GW2 process");
        p!.Kill(true);
        killed = true;
        await Task.Delay(200);
        return true;
    }

    private void Gw2Exited(object? sender, EventArgs e)
    {
        ExitReason = closed ? ExitReason.Success : ExitReason.Crashed;

        switch (runStatus)
        {
            case RunState.Completed:
                ExitReason = ExitReason.Success;
                break;
            default:
                break;
        }

        switch (runStage)
        {
            case RunStage.LoginFailed:
                ExitReason = ExitReason.LoginFailed;
                break;
        }

        ChangeRunStage(RunStage.Exited, "Process.Exit event");
        Logger.Debug("{0} GW2 process exited because {1}", Account.Name, ExitReason);
        launchLogger?.Info("{0} GW2 process exited because {1}", Account.Name, ExitReason);
        AccountLogger?.Debug("GW2 process exited because {1}", Account.Name, ExitReason);
        if (!killed && launchType != LaunchType.Update)
        {
            AccountLogger?.Debug("This exit was unexpected");
        }
        ExitAt = DateTime.UtcNow;
    }

    public bool Equals(Client? other) => Account.Equals(other?.Account) && AccountIndex.Equals(other?.AccountIndex);

}

