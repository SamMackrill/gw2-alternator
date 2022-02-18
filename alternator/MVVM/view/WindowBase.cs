namespace guildwars2.tools.alternator.MVVM.view;


public class WindowBase : Window
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public enum ResizeDirections
    {
        Both,
        HorizontalOnly,
        VerticalOnly,
    }

    public bool RememberPosition { get; set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (RememberPosition) this.ApplyPlacement();
    }

    private void ClosingTrigger(object? sender, CancelEventArgs e)
    {
        if (RememberPosition) this.SavePlacement();
    }

    protected override void OnInitialized(EventArgs e)
    {
        if (Content is IAddChild top && ResizeMode == ResizeMode.CanResize) AddResizeHandles(top);
        Closing += ClosingTrigger;

        base.OnInitialized(e);
    }

    public ResizeDirections ResizeOrientation { get; set; } = ResizeDirections.Both;

    private const int CornerGripSize = 12;
    private const int SideGripSize = 7;
    private void AddResizeHandles(IAddChild parent)
    {
        var style = Resources["RectBorderStyle"] as Style;
        var fill = new SolidColorBrush(Colors.Transparent);

        System.Windows.Shapes.Rectangle BaseRectangle()
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Style = style,
                Focusable = false,
                Fill = fill,
                Tag = this,
            };
            rect.MouseLeftButtonDown += Resize_Init;
            rect.MouseLeftButtonUp += Resize_End;
            rect.MouseMove += Resizing_Form;
            return rect;
        }

        if (ResizeOrientation != ResizeDirections.VerticalOnly)
        {
            var leftSizeGrip = BaseRectangle();
            leftSizeGrip.Name = "LeftSizeGrip";
            leftSizeGrip.Width = SideGripSize;
            leftSizeGrip.HorizontalAlignment = HorizontalAlignment.Left;
            leftSizeGrip.Cursor = Cursors.SizeWE;
            parent.AddChild(leftSizeGrip);

            var rightSizeGrip = BaseRectangle();
            rightSizeGrip.Name = "RightSizeGrip";
            rightSizeGrip.Width = SideGripSize;
            rightSizeGrip.HorizontalAlignment = HorizontalAlignment.Right;
            rightSizeGrip.Cursor = Cursors.SizeWE;
            parent.AddChild(rightSizeGrip);
        }

        if (ResizeOrientation != ResizeDirections.HorizontalOnly)
        {
            var topSizeGrip = BaseRectangle();
            topSizeGrip.Name = "TopSizeGrip";
            topSizeGrip.Height = SideGripSize;
            topSizeGrip.VerticalAlignment = VerticalAlignment.Top;
            topSizeGrip.Cursor = Cursors.SizeNS;
            parent.AddChild(topSizeGrip);

            var bottomSizeGrip = BaseRectangle();
            bottomSizeGrip.Name = "BottomSizeGrip";
            bottomSizeGrip.Height = SideGripSize;
            bottomSizeGrip.VerticalAlignment = VerticalAlignment.Bottom;
            bottomSizeGrip.Cursor = Cursors.SizeNS;
            parent.AddChild(bottomSizeGrip);
        }

        if (ResizeOrientation == ResizeDirections.Both)
        {
            var topLeftSizeGrip = BaseRectangle();
            topLeftSizeGrip.Name = "TopLeftSizeGrip";
            topLeftSizeGrip.Height = CornerGripSize;
            topLeftSizeGrip.Width = CornerGripSize;
            topLeftSizeGrip.HorizontalAlignment = HorizontalAlignment.Left;
            topLeftSizeGrip.VerticalAlignment = VerticalAlignment.Top;
            topLeftSizeGrip.Cursor = Cursors.SizeNWSE;
            parent.AddChild(topLeftSizeGrip);

            var topRightSizeGrip = BaseRectangle();
            topRightSizeGrip.Name = "TopRightSizeGrip";
            topRightSizeGrip.Height = CornerGripSize;
            topRightSizeGrip.Width = CornerGripSize;
            topRightSizeGrip.HorizontalAlignment = HorizontalAlignment.Right;
            topRightSizeGrip.VerticalAlignment = VerticalAlignment.Top;
            topRightSizeGrip.Cursor = Cursors.SizeNESW;
            parent.AddChild(topRightSizeGrip);

            var bottomRightSizeGrip = BaseRectangle();
            bottomRightSizeGrip.Name = "BottomRightSizeGrip";
            bottomRightSizeGrip.Height = CornerGripSize;
            bottomRightSizeGrip.Width = CornerGripSize;
            bottomRightSizeGrip.HorizontalAlignment = HorizontalAlignment.Right;
            bottomRightSizeGrip.VerticalAlignment = VerticalAlignment.Bottom;
            bottomRightSizeGrip.Cursor = Cursors.SizeNWSE;
            parent.AddChild(bottomRightSizeGrip);

            var bottomLeftSizeGrip = BaseRectangle();
            bottomLeftSizeGrip.Name = "BottomLeftSizeGrip";
            bottomLeftSizeGrip.Height = CornerGripSize;
            bottomLeftSizeGrip.Width = CornerGripSize;
            bottomLeftSizeGrip.HorizontalAlignment = HorizontalAlignment.Left;
            bottomLeftSizeGrip.VerticalAlignment = VerticalAlignment.Bottom;
            bottomLeftSizeGrip.Cursor = Cursors.SizeNESW;
            parent.AddChild(bottomLeftSizeGrip);
        }
    }


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
    private void Resize_Init(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Rectangle senderRect) return;
        //Logger.Debug("Resize_Init {0}", senderRect.Name);
        resizeInProcess = true;
        senderRect.CaptureMouse();
    }

    private void Resize_End(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Rectangle senderRect) return;
        //Logger.Debug("Resize_End {0}", senderRect.Name);
        resizeInProcess = false;
        senderRect.ReleaseMouseCapture();
    }

    private void Resizing_Form(object sender, MouseEventArgs e)
    {
        var senderRect = sender as System.Windows.Shapes.Rectangle;
        //Logger.Debug("Resizing_Form {0} resizeInProcess={1}", senderRect.Name, resizeInProcess);

        if (!resizeInProcess) return;
        if (senderRect?.Tag is not Window window) return;

        window.SizeToContent = SizeToContent.Manual;

        var mousePosition = System.Windows.Forms.Control.MousePosition;
        var position = window.PointFromScreen(new System.Windows.Point(mousePosition.X, mousePosition.Y));

        var width = position.X;
        var height = position.Y;
        senderRect.CaptureMouse();

        var senderRectName = senderRect.Name.ToLowerInvariant();

        e.Handled = (DragRight(senderRectName, width, window) || DragLeft(senderRectName, width, window))
                  | (DragDown(senderRectName, height, window) || DragUp(senderRectName, height, window));
    }

    private const double Offset = 7;

    private bool DragUp(string senderRectName, double y, Window window)
    {
        if (!senderRectName.Contains("top")) return false;

        y -= Offset;
        var newHeight = window.Height - y;
        if (newHeight <= 0 || newHeight < window.MinHeight || newHeight > window.MaxHeight) return false;

        window.Top += y;
        window.Height = newHeight;
        return true;
    }

    private bool DragDown(string senderRectName, double y, Window window)
    {
        if (!senderRectName.Contains("bottom")) return false;

        var newHeight = y + Offset;
        if (newHeight <= 0 || newHeight < window.MinHeight || newHeight > window.MaxHeight) return false;

        window.Height = newHeight;
        return true;
    }

    private bool DragLeft(string senderRectName, double x, Window window)
    {
        if (!senderRectName.Contains("left")) return false;
        
        x -= Offset;
        var newWidth = window.Width - x;
        if (newWidth <= 0 || newWidth < window.MinWidth || newWidth > window.MaxWidth) return false;

        window.Left += x;
        window.Width = newWidth;
        return true;
    }

    private bool DragRight(string senderRectName, double x, Window window)
    {
        if (!senderRectName.Contains("right")) return false;
        
        var newWidth = x + Offset;
        if (newWidth <= 0 || newWidth < window.MinWidth || newWidth > window.MaxWidth) return false;

        window.Width = newWidth;
        return true;
    }
}