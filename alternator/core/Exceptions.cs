using System.Runtime.Serialization;

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

    protected Gw2Exception(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
