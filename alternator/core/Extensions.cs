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

}