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
    private string ProcessId => p?.Id.ToString() ?? "?";

    private long lastStageMemoryUsage;
    private DateTime lastStageSwitchTime;
    private int lastStageModuleCount;

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
    public event EventHandler<EventArgs>? MutexDeleted;

    private string? failedReason;

    private string DebugDisplay => $"{Account.Name ?? "Unset"} Status:{RunStatus} Stage:{RunStage} {failedReason ?? ""}";

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
            AccountLogger?.Debug("RunStatus: {0}", runStatus);
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
            AccountLogger?.Debug("RunStage: {0}", runStage);
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
            if (RunStage != RunStage.CharacterSelectReached) return;

            var stageDiff = Math.Abs(memoryUsage - lastStageMemoryUsage);
            if (stageDiff < settings.DeltaMemoryThreshold) return;
            AccountLogger?.Debug("CheckIfMovedOn={1} ({2}>{3})", Account.Name, memoryUsage, stageDiff, settings.DeltaMemoryThreshold);

            await ChangeRunStage(RunStage.CharacterSelected, 200,
                $"Memory increased by {stageDiff} > {settings.DeltaMemoryThreshold}", cancellationToken);
        }

        async Task CheckMemoryThresholdReached(long diff, long memoryUsage)
        {
            if (RunStage is not (RunStage.Authenticated or RunStage.CharacterSelected)) return;

            if (diff >= tuning.MinDiff) return;

            AccountLogger?.Debug("Memory={1} ({2}<{3})", Account.Name, memoryUsage, diff, tuning.MinDiff);
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

        async Task CheckIfCrashed()
        {
            if (RunStage is not (RunStage.Playing)) return;

            var switchTime = DateTime.UtcNow.Subtract(lastStageSwitchTime);
            if (switchTime.TotalSeconds < settings.CrashWaitDelay) return;

            if (loadedModules.Count > lastStageModuleCount) return;

            failedReason = $"Crashed state detected no modules loaded within {settings.CrashWaitDelay}s)";

            Logger.Debug("{0} failed awaiting login (because: {1})", Account.Name, failedReason);
            AccountLogger?.Debug("Failed awaiting login (because: {1})", Account.Name, failedReason);

            await ChangeRunStage(RunStage.LoginCrashed, 20, failedReason, cancellationToken);
        }

        Dictionary<RunStage, List<string>> runStageFromModules = new()
        {
            { RunStage.Authenticated,          new List<string> { @"winnsi.dll", @"nsi.dll" } }, // Windows Network Store Information
            { RunStage.CharacterSelectReached, new List<string> { @"lglcdapi.dll" } }, // Microsoft Color Management System
            { RunStage.Playing,                new List<string> { @"nan.dll" } }, // C++ runtime library
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

        await Start(cancellationToken);

        if (launchType is not LaunchType.Update)
        {
            await KillMutex(settings.StartDelay, 200);
            MutexDeleted?.Invoke(this, EventArgs.Empty);
        }

        var timeout = launchType == LaunchType.Collect ? TimeSpan.MaxValue : new TimeSpan(0, 0, settings.LaunchTimeout);
        // State Engine
        while (Alive)
        {
            if (DateTime.UtcNow.Subtract(StartAt) > timeout)
            {
                Logger.Debug("{0} Timed-out after {1}s, giving up", Account.Name, timeout.TotalSeconds);
                launchLogger?.Info("{0} Timed-out after {1}s, giving up", Account.Name, timeout.TotalSeconds);
                AccountLogger?.Debug("Timed-out after {1}s, giving up", Account.Name, timeout.TotalSeconds);
                await Shutdown(0);
                throw new Gw2TimeoutException("GW2 process timed-out");
            }

            if (nextEnterKeyRequest < DateTime.Now)
            {
                var stageDiff = Math.Abs(p.WorkingSet64 / 1024 - lastStageMemoryUsage);
                AccountLogger?.Info("Memory delta {0}", stageDiff);
                SendEnterKey(true, "engine");
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
        //nextEnterKeyRequest = DateTime.MaxValue;
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
        lastStageModuleCount = loadedModules.Count;
        UpdateEngineSpeed();
        RunStatusChanged?.Invoke(this, eventArgs);
    }



    private async Task Start(CancellationToken cancellationToken)
    {
        if (!p!.Start()) throw new Gw2Exception($"{Account.Name} Failed to start");

        RunStatus = RunState.Running;
        StartAt = p.StartTime.ToUniversalTime();
        Logger.Debug("{0} Started {1}", Account.Name, launchType);
        launchLogger?.Info("{0} Started {1} process={2}", Account.Name, launchType, ProcessId);
        AccountLogger?.Debug("Started {1} process={2}", Account.Name, launchType, ProcessId);
        await ChangeRunStage(RunStage.Started, 200, "Normal start", cancellationToken);
    }

    public void SelectCharacter()
    {
        SendEnterKey(false, "SelectCharacter");
    }

    private async Task KillMutex(int delayBefore, int delayAfter)
    {
        if (!Alive) return;

        try
        {
            launchLogger?.Info("{0} KillMutex: WaitForInputIdle on process={1}", Account.Name, ProcessId);
            AccountLogger?.Info("KillMutex: WaitForInputIdle on process={1}", Account.Name, ProcessId);
            p!.WaitForInputIdle();

            if (!Alive) return;

            AccountLogger?.Info("KillMutex: Delay", Account.Name);
            await Task.Delay(delayBefore);

            if (!Alive) return;

            AccountLogger?.Info("KillMutex: GetHandle", Account.Name);
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

            AccountLogger?.Info("{0} Got handle to Mutex", Account.Name);
            handle.Kill();
            Logger.Debug("{0} Killed Mutex", Account.Name);
            launchLogger?.Info("{0} Killed Mutex", Account.Name);
            AccountLogger?.Debug("Killed Mutex", Account.Name);
            await Task.Delay(delayAfter);
        }
        catch (Exception e)
        {
            Logger.Error(e, "{0} error killing Mutex, ignoring", Account.Name);
            launchLogger?.Info("{0} error killing Mutex, ignoring", Account.Name);
            AccountLogger?.Error(e, "error killing Mutex, ignoring", Account.Name);
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
                if (!p.HasExited) return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "{0} Error checking if process alive", Account.Name);
                AccountLogger?.Debug(e, "Error checking if process alive", Account.Name);
            }
            AccountLogger?.Info("Process {1} is Dead", Account.Name, ProcessId);
            return false;
        }
    }

    private DateTime nextEnterKeyRequest = DateTime.MaxValue;

    public void SendEnterKey(bool repeat, string source)
    {
        if (!Alive) return;

        Logger.Debug("{0} SendEnterKey from {1}", Account.Name, source);
        AccountLogger?.Debug("SendEnterKey from {1}", Account.Name, source);

        nextEnterKeyRequest = DateTime.MaxValue;

        Logger.Debug("{0} Send ENTER", Account.Name);
        AccountLogger?.Debug("Send ENTER", Account.Name);

        var currentFocus = Native.GetForegroundWindow();
        try
        {
            _ = Native.SetForegroundWindow(p!.MainWindowHandle);
            InputSender.ClickKey(0x1c); // Enter
            
            if (!repeat) return;

            nextEnterKeyRequest = DateTime.Now.AddSeconds(2);
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

        launchLogger?.Debug("{0} Kill GW2 process={1}", Account.Name, ProcessId);
        AccountLogger?.Debug("Kill GW2 process={1}", Account.Name, ProcessId);
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
        AccountLogger?.Debug("GW2 process {2} exited because {1}", Account.Name, ExitReason, ProcessId);
        if (!killed && launchType != LaunchType.Update)
        {
            AccountLogger?.Debug("This exit was unexpected");
        }
        ExitAt = DateTime.UtcNow;
    }

    public bool Equals(Client? other) => Account.Equals(other?.Account) && AccountIndex.Equals(other?.AccountIndex);

}

