using System;

namespace guildwars2.tools.alternator
{
    public static class Extensions
    {
        public static long GetValue(this IntPtr ptr)
        {
            return (long)ptr;
        }

        public static int GetValue32(this IntPtr ptr)
        {
            return (int)(long)ptr;
        }

        public static ulong GetValue(this UIntPtr ptr)
        {
            return (ulong)ptr;
        }
    }
}
