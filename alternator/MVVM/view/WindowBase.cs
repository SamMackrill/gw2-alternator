using System.Windows.Input;
using System.Windows.Shapes;

namespace guildwars2.tools.alternator.MVVM.view
{
    public class WindowBase : Window
    {
        private protected void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
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
            if (sender is not Rectangle senderRect) return;
            resizeInProcess = true;
            senderRect.CaptureMouse();
        }

        private protected void Resize_End(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Rectangle senderRect) return;
            resizeInProcess = false;
            senderRect.ReleaseMouseCapture();
        }

        private protected void Resizing_Form(object sender, MouseEventArgs e)
        {
            if (!resizeInProcess) return;
            var senderRect = sender as Rectangle;
            if (senderRect?.Tag is not Window mainWindow) return;


            var position = e.GetPosition(mainWindow);
            var width = position.X;
            var height = position.Y;
            senderRect.CaptureMouse();
            if (senderRect.Name.Contains("right", StringComparison.OrdinalIgnoreCase))
            {
                width += 5;
                if (width > 0)
                {
                    mainWindow.Width = width;
                    e.Handled = true;
                }
            }
            if (senderRect.Name.Contains("left", StringComparison.OrdinalIgnoreCase))
            {
                width -= 5;
                var widthDiff = mainWindow.Width - width;
                if (widthDiff > mainWindow.MinWidth && widthDiff < mainWindow.MaxWidth)
                {
                    mainWindow.Width = widthDiff;
                    mainWindow.Left += width;
                    e.Handled = true;
                }
            }
            if (senderRect.Name.Contains("bottom", StringComparison.OrdinalIgnoreCase))
            {
                height += 5;
                if (height > 0)
                {
                    mainWindow.Height = height;
                    e.Handled = true;
                }
            }
            if (senderRect.Name.Contains("top", StringComparison.OrdinalIgnoreCase))
            {
                height -= 5;
                var heightDiff = mainWindow.Height - height;
                if (heightDiff > mainWindow.MinHeight && heightDiff < mainWindow.MaxHeight)
                {
                    mainWindow.Height = heightDiff;
                    mainWindow.Top += height;
                    e.Handled = true;
                }
            }
        }

    }
}
