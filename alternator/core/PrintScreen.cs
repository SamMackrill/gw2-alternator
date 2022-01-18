namespace guildwars2.tools.alternator;

public static class PrintScreen
{
    /// <summary>
    /// Creates an Bitmap containing a screen shot of a specific window
    /// </summary>
    /// <param name="handle">The handle to the window. (In windows forms, this is obtained by the Handle property)</param>
    /// <param name="part"></param>
    /// <returns>Bitmap</returns>
    public static Bitmap CaptureWindow(IntPtr handle)
    {
        var windowRect = new RECT();
        User32.GetWindowRect(handle, ref windowRect);

        var regionSize = new System.Drawing.Size(windowRect.Width, windowRect.Height);
        var bitmap = new Bitmap(regionSize.Width, regionSize.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.CopyFromScreen(windowRect.left, windowRect.top, 0, 0, regionSize);
        return bitmap;
    }

    public static Color CaptureWindowPixel(IntPtr handle, System.Drawing.Point position)
    {
        var windowRect = new RECT();
        User32.GetWindowRect(handle, ref windowRect);
        var regionSize = new System.Drawing.Size(1, 1);

        using var bitmap = new Bitmap(regionSize.Width, regionSize.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.CopyFromScreen(windowRect.left + position.X, windowRect.top + position.Y, 0, 0, regionSize);
        return bitmap.GetPixel(0,0);
    }

}
