namespace guildwars2.tools.alternator;

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

    public static bool IsTheSameAs(this byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;

        for (var i = a.Length - 1; i >= 0; i--)
        {
            if (a[i] != b[i]) return false;
        }

        return true;
    }
}