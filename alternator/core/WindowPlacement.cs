using System.Windows.Interop;
using System.Xml;
using System.Xml.Serialization;

namespace guildwars2.tools.alternator;

// RECT structure required by WINDOWPLACEMENT structure
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}

// POINT structure required by WINDOWPLACEMENT structure
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }
}

// WINDOWPLACEMENT stores the position, size, and state of a window
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct WINDOWPLACEMENT
{
    public int length;
    public int flags;
    public int showCmd;
    public POINT minPosition;
    public POINT maxPosition;
    public RECT normalPosition;
}

public static class WindowPlacement
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly Encoding Encoding;
    private static readonly XmlSerializer Serializer;

    static WindowPlacement()
    {
        Serializer = new XmlSerializer(typeof(WINDOWPLACEMENT));
        Encoding = new UTF8Encoding();
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;

    private static void SetPlacement(IntPtr windowHandle, string placementXml)
    {
        if (string.IsNullOrEmpty(placementXml)) return;

        var xmlBytes = Encoding.GetBytes(placementXml);

        try
        {
            using var memoryStream = new MemoryStream(xmlBytes);
            var deserialize = Serializer.Deserialize(memoryStream);
            if (deserialize is not WINDOWPLACEMENT placement) return;

            placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            placement.flags = 0;
            placement.showCmd = placement.showCmd == SW_SHOWMINIMIZED ? SW_SHOWNORMAL : placement.showCmd;
            SetWindowPlacement(windowHandle, ref placement);
        }
        catch (InvalidOperationException)
        {
            // Parsing placement XML failed. Fail silently.
        }
    }

    private static string GetPlacement(IntPtr windowHandle)
    {
        GetWindowPlacement(windowHandle, out var placement);

        using var memoryStream = new MemoryStream();
        using var xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
        Serializer.Serialize(xmlTextWriter, placement);
        var xmlBytes = memoryStream.ToArray();
        return Encoding.GetString(xmlBytes);
    }

    private static string FilePath(string name) => Path.Combine(App.ApplicationFolder, name);

    public static void ApplyPlacement(this Window window)
    {
        var className = window.GetType().Name;
        try
        {
            var filePath = FilePath($"{className}_pos.xml");
            if (!File.Exists(filePath)) return;
            var pos = File.ReadAllText(filePath);
            SetPlacement(new WindowInteropHelper(window).Handle, pos);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Couldn't read position for {0}", className);
        }
    }

    public static void SavePlacement(this Window window)
    {
        var className = window.GetType().Name;
        var pos = GetPlacement(new WindowInteropHelper(window).Handle);
        try
        {
            File.WriteAllText(FilePath($"{className}_pos.xml"), pos);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Couldn't write position for {0}", className);
        }
    }
}