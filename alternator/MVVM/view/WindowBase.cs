namespace guildwars2.tools.alternator.MVVM.view;

public class WindowBase : Window
{
    private protected void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch 
        {
            // Ignore weird windows behaviour
        }
    }

    private protected void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private protected void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private bool resizeInProcess;
    private protected void Resize_Init(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Rectangle senderRect) return;
        resizeInProcess = true;
        senderRect.CaptureMouse();
    }

    private protected void Resize_End(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Rectangle senderRect) return;
        resizeInProcess = false;
        senderRect.ReleaseMouseCapture();
    }

    private protected void Resizing_Form(object sender, MouseEventArgs e)
    {
        if (!resizeInProcess) return;
        var senderRect = sender as System.Windows.Shapes.Rectangle;
        if (senderRect?.Tag is not Window window) return;


        //var position = e.GetPosition(mainWindow); // This only works on the Main window, d'oh
        var mousePosition = System.Windows.Forms.Control.MousePosition;
        var position = window.PointFromScreen(new System.Windows.Point(mousePosition.X, mousePosition.Y));

        var width = position.X;
        var height = position.Y;
        senderRect.CaptureMouse();

        var senderRectName = senderRect.Name.ToLowerInvariant();

        e.Handled = (DragRight(senderRectName, width, window) || DragLeft(senderRectName, width, window))
                  | (DragDown(senderRectName, height, window) || DragUp(senderRectName, height, window));
    }

    private bool DragUp(string senderRectName, double y, Window window)
    {
        if (!senderRectName.Contains("top")) return false;

        y -= 5;
        var newHeight = window.Height - y;
        if (newHeight <= 0 || newHeight < window.MinHeight || newHeight > window.MaxHeight) return false;

        window.Top += y;
        window.Height = newHeight;
        return true;
    }

    private bool DragDown(string senderRectName, double y, Window window)
    {
        if (!senderRectName.Contains("bottom")) return false;

        var newHeight = y + 5;
        if (newHeight <= 0 || newHeight < window.MinHeight || newHeight > window.MaxHeight) return false;

        window.Height = newHeight;
        return true;
    }

    private bool DragLeft(string senderRectName, double x, Window window)
    {
        if (!senderRectName.Contains("left")) return false;
        
        x -= 5;
        var newWidth = window.Width - x;
        if (newWidth <= 0 || newWidth < window.MinWidth || newWidth > window.MaxWidth) return false;

        window.Left += x;
        window.Width = newWidth;
        return true;
    }

    private bool DragRight(string senderRectName, double x, Window window)
    {
        if (!senderRectName.Contains("right")) return false;
        
        var newWidth = x + 5;
        if (newWidth <= 0 || newWidth < window.MinWidth || newWidth > window.MaxWidth) return false;

        window.Width = newWidth;
        return true;
    }
}