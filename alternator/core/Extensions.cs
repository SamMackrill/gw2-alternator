using System.Security;

namespace guildwars2.tools.alternator;

public static class Extensions
{
    public static long GetValue(this IntPtr ptr)
    {
        return (long) ptr;
    }

    public static int GetValue32(this IntPtr ptr)
    {
        return (int) (long) ptr;
    }

    public static ulong GetValue(this UIntPtr ptr)
    {
        return (ulong) ptr;
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

    public static void PassOnChanges(this PropertyChangedEventArgs args, Action<string> onPropertyChanged, Dictionary<string, List<string>>? propertyConverter = null)
    {
        var propertyName = args.PropertyName;
        if (propertyName == null) return;

        var propertyNames = new List<string> {propertyName};
        if (propertyConverter != null && propertyConverter.ContainsKey(propertyName)) propertyNames.AddRange(propertyConverter[propertyName]);
        foreach (var name in propertyNames)
        {
            onPropertyChanged(name);
        }
    }

    public static string GetSafeFileName(this string name, char replace = '_')
    {
        var invalids = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalids.Contains(c) ? replace : c).ToArray());
    }

    public static string SplitCamelCase(this string input)
    {
        return Regex.Replace(input, "([A-Z])", " $1", RegexOptions.Compiled).Trim();
    }

    private static string? cancellationReason;

    public static void Cancel(
        this CancellationTokenSource cancellationTokenSource,
        string reason
    )
    {
        cancellationReason = reason;
        cancellationTokenSource.Cancel();
    }

    public static void Cancel(
        this CancellationTokenSource cancellationTokenSource,
        bool throwOnFirstException,
        string reason
    )
    {
        cancellationReason = reason;
        cancellationTokenSource.Cancel(throwOnFirstException);
    }

    public static string CancellationReason(
        this CancellationToken ct)
    {
        return cancellationReason ?? "unknown";
    }



    public static void Resume(this Process process)
    {
        if (process.HasExited) return;

        void resume(ProcessThread pt)
        {
            var threadHandle = Native.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pt.Id);

            if (threadHandle == IntPtr.Zero) return;
            try { _ = Native.ResumeThread(threadHandle); }
            finally { _ = Native.CloseHandle(threadHandle); }
        }

        var threads = process.Threads.Cast<ProcessThread>().ToArray();

        if (threads.Length > 1)
        {
            Parallel.ForEach(threads,
                new ParallelOptions { MaxDegreeOfParallelism = threads.Length },
                resume);
        }
        else resume(threads[0]);
    }

    public static void Suspend(this Process process)
    {
        if (process.HasExited) return;

        void suspend(ProcessThread pt)
        {
            var threadHandle = Native.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pt.Id);

            if (threadHandle == IntPtr.Zero)
            {
                return;
            }

            try { _ = Native.SuspendThread(threadHandle); }
            finally { _ = Native.CloseHandle(threadHandle); }
        }

        var threads = process.Threads.Cast<ProcessThread>().ToArray();

        if (threads.Length > 1)
        {
            Parallel.ForEach(threads,
                new ParallelOptions { MaxDegreeOfParallelism = threads.Length },
                suspend);
        }
        else suspend(threads[0]);
    }


}