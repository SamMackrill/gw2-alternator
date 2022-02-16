namespace guildwars2.tools.alternator.MVVM.viewmodel;

[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class AccountVpnViewModel : ObservableObject
{
    private IAccount account;

    private bool isChecked;
    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (SetProperty(ref isChecked, value)) account.UpdateVpn(vpn, isChecked);
        }
    }

    public string Id => vpn.Id ?? string.Empty;
    public string Display => vpn.ToString();

    private readonly VpnDetails vpn;

    public AccountVpnViewModel(VpnDetails vpn, IAccount account)
    {
        this.vpn = vpn;
        this.account = account;
        IsChecked = account.Vpns?.Contains(Id) ?? false;
    }

    private string DebugDisplay => $"{Id} {IsChecked}";

}