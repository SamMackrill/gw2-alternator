using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace alternator.core
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
