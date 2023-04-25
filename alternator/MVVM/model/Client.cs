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

    private DateTime lastStageSwitchTime;
    private int lastStageModuleCount;

    private bool closed;
    private bool killed;
    private LaunchType launchType;
    private VpnDetails vpn;
    private record struct EngineTuning(
        TimeSpan Pause, 
        long LastMemoryUsage, 
        long LastStageMemoryUsage, 
        long MinDiff, 
        TimeSpan StuckDelay, 
        int StuckTolerance, 
        long LoopDiff, 
        long StageDiff,
        int StageEnterCount
    );

    private EngineTuning tuning;

    private record struct FeatureFlag(
         bool ManualStep,
         bool DoNotSendEnter,
         bool DoNotTimeout
    );

    private FeatureFlag featureFlag = new(ManualStep:false, DoNotSendEnter:false, DoNotTimeout:false);


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
        VpnDetails vpn,
        CancellationToken cancellationToken
        )
    {
        this.launchType = launchType;
        this.vpn = vpn;
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

        async Task CheckIfMemoryThresholdReached()
        {
            switch (RunStage)
            {
                case RunStage.ReadyToLogin when tuning.StageDiff > settings.AuthenticationMemoryThreshold:
                    AccountLogger?.Debug("{3} Threshold(kB): {1:n0}>{2:n0}", Account.Name, tuning.StageDiff, settings.AuthenticationMemoryThreshold, RunStage);
                    await ChangeRunStage(RunStage.ReadyToPlay, $"Memory(kB) {tuning.StageDiff} > {settings.AuthenticationMemoryThreshold}", 200, cancellationToken);
                    break;
                case RunStage.Playing when tuning.StageDiff > settings.CharacterSelectionMemoryThreshold * 1024:
                    AccountLogger?.Debug("{3} Threshold(MB): {1:n0}>{2:n0}", Account.Name, tuning.StageDiff / 1024, settings.CharacterSelectionMemoryThreshold, RunStage);
                    await ChangeRunStage(RunStage.CharacterSelection, $"Memory(MB) {tuning.StageDiff / 1024} > {settings.CharacterSelectionMemoryThreshold}", 200, cancellationToken);
                    break;
                case RunStage.CharacterSelection when tuning.StageDiff > settings.CharacterSelectedMemoryThreshold * 1024:
                    AccountLogger?.Debug("{3} Threshold(MB): {1:n0}>{2:n0}", Account.Name, tuning.StageDiff / 1024, settings.CharacterSelectedMemoryThreshold, RunStage);
                    await ChangeRunStage(RunStage.CharacterSelected, $"Memory(MB) {tuning.StageDiff / 1024} > {settings.CharacterSelectedMemoryThreshold}", 200, cancellationToken);
                    break;
                case RunStage.CharacterSelected when tuning.StageDiff > settings.WorldEnteredMemoryThreshold * 1024:
                    AccountLogger?.Debug("{3} Threshold(MB): {1:n0}>{2:n0}", Account.Name, tuning.StageDiff / 1024, settings.WorldEnteredMemoryThreshold, RunStage);
                    await ChangeRunStage(RunStage.WorldEntered, $"Memory(MB) {tuning.StageDiff / 1024} > {settings.WorldEnteredMemoryThreshold}", 1800, cancellationToken);
                    break;
            }
        }

        async Task CheckIfStuck(long memoryUsage)
        {
            if (RunStage is not (RunStage.ReadyToLogin or RunStage.ReadyToPlay or RunStage.Playing)) return;

            if (tuning.LoopDiff >= tuning.StuckTolerance) return; // still doing something

            var switchTime = DateTime.UtcNow.Subtract(lastStageSwitchTime);
            if (switchTime < tuning.StuckDelay) return;

            failedReason = $"Stuck, took too long ({switchTime.TotalSeconds:F1}s>{tuning.StuckDelay.TotalSeconds}s)";

            Logger.Debug("{0} failed awaiting login, mem={1:n0} diff={2:n0} (because: {3})", Account.Name, memoryUsage, tuning.LoopDiff, failedReason);
            AccountLogger?.Debug("Failed awaiting login, mem={1:n0} diff={2:n0} (because: {3})", Account.Name, memoryUsage, tuning.LoopDiff, failedReason);

            //CaptureWindow(RunStage.EntryFailed, applicationFolder);
            await ChangeRunStage(RunStage.LoginFailed, failedReason, 20, cancellationToken);
        }

        async Task CheckIfCrashed()
        {
            switch (RunStage)
            {
                case RunStage.ReadyToLogin or RunStage.ReadyToPlay:
                    if (tuning.StageEnterCount <= settings.MaxEnterRetries) return;

                    failedReason = $"Crashed state detected no progress after {tuning.StageEnterCount} enter key presses";

                    Logger.Debug("{0} failed at {2} (because: {1})", Account.Name, failedReason, RunStage);
                    AccountLogger?.Debug("Failed at {2} (because: {1})", Account.Name, failedReason, RunStage);

                    await ChangeRunStage(RunStage.LoginCrashed, failedReason, 20, cancellationToken);
                    break;
                case RunStage.Playing:
                    var switchTime = DateTime.UtcNow.Subtract(lastStageSwitchTime);
                    if (switchTime.TotalSeconds < settings.CrashWaitDelay) return;

                    if (loadedModules.Count > lastStageModuleCount) return;

                    failedReason = $"Crashed state detected no modules loaded within {settings.CrashWaitDelay}s)";

                    Logger.Debug("{0} failed at {2} (because: {1})", Account.Name, failedReason, RunStage);
                    AccountLogger?.Debug("Failed at {2} (because: {1})", Account.Name, failedReason, RunStage);

                    await ChangeRunStage(RunStage.LoginCrashed, failedReason, 20, cancellationToken);
                    break;
            }
        }

        Dictionary<RunStage, List<string>> runStageFromModules = new()
        {
            { RunStage.ReadyToLogin,       new List<string> { @"umpdc.dll", @"powrprof.dll" } }, // Windows Network Store Information
            { RunStage.Playing,            new List<string> { @"d3d11.dll" } }, // DX11
            { RunStage.CharacterSelection, new List<string> { @"nan.dll" } }, // can't find one :(
        };


        async Task CheckIfStageUpdated(long memoryUsage)
        {
            if (launchType == LaunchType.Update) return;

            await CheckIfMemoryThresholdReached();

            if (!featureFlag.DoNotTimeout) await CheckIfStuck(memoryUsage);


            var newModules = UpdateProcessModules();
            foreach (var runStageModules in runStageFromModules)
            {
                var module = runStageModules.Value.FirstOrDefault(module => newModules.Contains(module));
                if (module == null) continue;
                await ChangeRunStage(runStageModules.Key, $"Module {module} loaded", 200, cancellationToken);
                return;
            }
        }

        string FormArguments(bool share)
        {
            if (launchType is LaunchType.Update) return "-image";

            var args = "-windowed -nosound -maploadinfo -fps 20 -autologin";
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
        var startingMemory = p.WorkingSet64 / 1024; // kB
        tuning = new EngineTuning(new TimeSpan(0, 0, 0, 0, 200), startingMemory, startingMemory, 1L, new TimeSpan(0, 0, settings.StuckTimeout), 100, 0L, 0L, 0);


        if (launchType is not LaunchType.Update)
        {
            await KillMutex(settings.StartDelay, 200);
            MutexDeleted?.Invoke(this, EventArgs.Empty);
        }

        var timeout = launchType == LaunchType.Collect ? TimeSpan.MaxValue : new TimeSpan(0, 0, settings.LaunchTimeout);
        // State Engine
        while (Alive)
        {
            if (!featureFlag.DoNotTimeout && DateTime.UtcNow.Subtract(StartAt) > timeout)
            {
                Logger.Debug("{0} Timed-out after {1}s, giving up", Account.Name, timeout.TotalSeconds);
                launchLogger?.Info("{0} Timed-out after {1}s, giving up", Account.Name, timeout.TotalSeconds);
                AccountLogger?.Debug("Timed-out after {1}s, giving up", Account.Name, timeout.TotalSeconds);
                await Shutdown(0);
                throw new Gw2TimeoutException("GW2 process timed-out");
            }

            var memoryUsage = p.WorkingSet64 / 1024; // kB
            tuning.LoopDiff = memoryUsage - tuning.LastMemoryUsage;
            tuning.StageDiff = memoryUsage - tuning.LastStageMemoryUsage;
            if (tuning.LoopDiff > 5)
            {
                AccountLogger?.Debug("Memory(kB) current={1:n0} last={2:n0} Δ={3:n0}", Account.Name, memoryUsage, tuning.LastMemoryUsage, tuning.LoopDiff);
                AccountLogger?.Debug("Stage(kB)  current={1:n0} last={2:n0} Δ={3:n0}", Account.Name, memoryUsage, tuning.LastStageMemoryUsage, tuning.StageDiff);
            }

            if (nextEnterKeyRequest < DateTime.Now)
            {
                AccountLogger?.Info("Enter Request StageΔ {0:n0}kB", tuning.StageDiff);
                SendEnterKey(true, "engine");
                tuning.StageEnterCount++;
            }

            await CheckIfCrashed();

            await CheckIfStageUpdated(memoryUsage);

            tuning.LastMemoryUsage = memoryUsage;
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
            case >= RunStage.CharacterSelection:
                tuning.Pause = new TimeSpan(0, 0, 0, 2, 0);
                tuning.MinDiff = 200L;
                break;
        }
    }

    private async Task ChangeRunStage(RunStage newRunStage, string? reason, int delay, CancellationToken cancellationToken)
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
            case RunStage.ReadyToPlay:
                LoginAt = DateTime.UtcNow;
                break;
            case RunStage.CharacterSelected:
                EnterAt = DateTime.UtcNow;
                break;
        }

        if (featureFlag.ManualStep) PauseForAnalysis();

        var eventArgs = new ClientStateChangedEventArgs(RunStage, newRunStage);
        RunStage = newRunStage;
        if (!p!.HasExited) tuning.LastStageMemoryUsage = p!.WorkingSet64 / 1024;
        tuning.StageEnterCount = 0;
        AccountLogger?.Debug("lastStageMemoryUsage set: {1:n0}kB", Account.Name, tuning.LastStageMemoryUsage);
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
        if (vpn.IsReal) AccountLogger?.Debug("VPN {1}", Account.Name, vpn.DisplayId);

        await ChangeRunStage(RunStage.Started, "Normal Start", 200, cancellationToken);
    }

    public void SelectCharacter()
    {
        //AccountLogger?.Debug("SelectCharacter (send ENTER)", Account.Name);
        SendEnterKey(true, "SelectCharacter");
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

            AccountLogger?.Info("Got handle to Mutex", Account.Name);
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

        if (featureFlag.DoNotSendEnter)
        {
            AccountLogger?.Debug("Skip Send ENTER from {1}", Account.Name, source);
            return;
        }

        Logger.Debug("{0} Send ENTER from {1}", Account.Name, source);
        AccountLogger?.Debug("Send ENTER from {1}", Account.Name, source);

        if (featureFlag.ManualStep) PauseForAnalysis();

        nextEnterKeyRequest = DateTime.MaxValue;

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

    public void StopSendingEnter()
    {
        nextEnterKeyRequest = DateTime.MaxValue;
    }

    private void PauseForAnalysis()
    {
        if (!Debugger.IsAttached) return;

        AccountLogger?.Debug("GW2 Suspended", Account.Name);
        _ = Native.SetForegroundWindow(p!.MainWindowHandle);
        p?.Suspend();
        Debugger.Break();
        p?.Resume();
        AccountLogger?.Debug("GW2 Resumed", Account.Name);

    }

    public void MinimiseWindow()
    {
        if (featureFlag.ManualStep || featureFlag.DoNotSendEnter) return;

        AccountLogger?.Debug("GW2 Hide", Account.Name);
        _ = Native.ShowWindowAsync(p!.MainWindowHandle, ShowWindowCommands.ForceMinimize);
    }

    public void RestoreWindow()
    {
        if (featureFlag.ManualStep || featureFlag.DoNotSendEnter) return;

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

    public bool Equals(Client? other) => Account.Equals(other?.Account) && AccountIndex.Equals(other.AccountIndex);

}

