namespace guildwars2.tools.alternator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.ApplyPlacement();
    }

    private void ClosingTrigger(object? sender, CancelEventArgs e)
    {
        this.SavePlacement();
    }
}