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
    Waiting,
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