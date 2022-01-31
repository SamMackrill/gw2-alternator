namespace guildwars2.tools.alternator.MVVM.view;

/// <summary>
/// Interaction logic for Accounts.xaml
/// </summary>
public partial class Accounts : UserControl
{
    public Accounts()
    {
        InitializeComponent();
    }

    private void Vpn_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var textBlock = sender as TextBlock;
        if (textBlock == null) return;
        var row = textBlock.BindingGroup.Owner as DataGridRow;
        //row.BeginEdit();
    }
}