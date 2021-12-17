namespace guildwars2.tools.alternator;

public enum LaunchType
{
    LaunchAll,
    LaunchNeeded,
    CollectAll,
    CollectNeeded,
    UpdateAll,
}

public enum State
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