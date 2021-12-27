namespace guildwars2.tools.alternator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel;
        mainViewModel.RequestClose += Close;
        mainViewModel.RefreshWindow();
    }
}