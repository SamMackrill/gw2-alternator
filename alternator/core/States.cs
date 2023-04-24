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


public enum RunStage
{
    NotRun,
    Started,
    ReadyToLogin,
    LoginFailed,
    LoginCrashed,
    ReadyToPlay,
    Playing,
    CharacterSelection,
    EntryFailed,
    CharacterSelected,
    WorldEntered,
    Exited,
}

public enum ExitReason
{
    Unset,
    Success,
    LoginFailed,
    Crashed,
    Cancelled,
}