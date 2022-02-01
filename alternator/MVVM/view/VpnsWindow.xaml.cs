namespace guildwars2.tools.alternator.MVVM.view;

/// <summary>
/// Interaction logic for Vpns.xaml
/// </summary>
public partial class Vpns
{
    public Vpns()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void window_Loaded(object sender, RoutedEventArgs e)
    {
        MinWidth = ActualWidth;
        MinHeight = ActualHeight;
        MaxHeight = ActualHeight;
    }
}