namespace guildwars2.tools.alternator;

public enum LaunchType
{
    Login,
    Collect,
    Update,
}

public enum RunState
{
    Unset,
    Ready,
    WaitingForLoginSlot,
    WaitingForExeSlot,
    WaitingForAuthenticationThrottle,
    Running,
    Error,
    Completed,
    Cancelled,
}

public enum LaunchState
{
    Ready,
    UpToDate,
}


public enum RunStage
{
    NotRun,
    Started,
    Authenticated,
    LoginFailed,
    ReadyToPlay,
    Playing,
    CharacterSelectReached,
    EntryFailed,
    CharacterSelected,
    WorldEntered,
    Exited,
}