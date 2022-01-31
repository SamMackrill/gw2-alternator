namespace guildwars2.tools.alternator.MVVM.viewmodel;


[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class AccountVpnViewModel : ObservableObject
{
    public bool IsChecked { get; set; }
    public string Id => vpn.Id;
    public string Display => vpn.ToString();

    private readonly VpnDetails vpn;

    public AccountVpnViewModel(VpnDetails vpn, bool isChecked)
    {
        this.vpn = vpn;
        IsChecked = isChecked;
    }

    private string DebugDisplay => $"{Id} {IsChecked}";

}