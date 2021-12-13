using System;

namespace guildwars2.tools.alternator
{
    public class GenericEventArgs<T> : EventArgs
    {
        public T EventData { get; }

        public GenericEventArgs(T eventData)
        {
            EventData = eventData;
        }
    }
}
