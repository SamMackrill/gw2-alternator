namespace guildwars2.tools.alternator;

public class Gw2Exception : Exception
{
    public Gw2Exception()
    {
    }

    public Gw2Exception(string? message) : base(message)
    {
    }

    public Gw2Exception(string? message, Exception? innerException) : base(message, innerException)
    {
    }

}

public class Gw2MutexException : Gw2Exception
{
    public Gw2MutexException()
    {
    }

    public Gw2MutexException(string? message) : base(message)
    {
    }

    public Gw2MutexException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

}

public class Gw2CrashedException : Gw2Exception
{
    public Gw2CrashedException()
    {
    }

    public Gw2CrashedException(string? message) : base(message)
    {
    }

    public Gw2CrashedException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

}

public class Gw2TimeoutException : Gw2Exception
{
    public Gw2TimeoutException()
    {
    }

    public Gw2TimeoutException(string? message) : base(message)
    {
    }

    public Gw2TimeoutException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

}

public class Gw2NoAccountsException : Gw2Exception
{
    public Gw2NoAccountsException()
    {
    }

    public Gw2NoAccountsException(string? message) : base(message)
    {
    }

    public Gw2NoAccountsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

}
