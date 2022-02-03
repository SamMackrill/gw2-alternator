namespace guildwars2.tools.alternator.MVVM.view;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsWindow
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void window_Loaded(object sender, RoutedEventArgs e)
    {
        MinWidth = ActualWidth;
        MinHeight = ActualHeight;
        MaxHeight = ActualHeight;
    }
}