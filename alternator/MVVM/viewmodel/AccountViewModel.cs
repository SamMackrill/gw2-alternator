namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    private readonly VpnCollection vpnCollection;
    public IAccount Account { get;}

    public AccountViewModel(IAccount account, VpnCollection vpnCollection)
    {
        this.vpnCollection = vpnCollection;
        Account = account;
        account.PropertyChanged += ModelPropertyChanged;
        vpns = vpnCollection.GetAccountVpns(Account.VPN).OrderBy(v => v.Id).ToList();
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "Name", new() {"AccountName"} },
        { "LastLogin", new() { "Login"} },
        { "LastCollection", new() { "Collected"} },
        { "CreatedAt", new() { "Age"} },
        { "StatusMessage", new() { "TooltipText"} },
        { "VPN", new() { "VPNs"} },
    };

    private void ModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        var propertyNames = new List<string> { args.PropertyName };
        if (propertyConverter.ContainsKey(args.PropertyName)) propertyNames.AddRange(propertyConverter[args.PropertyName]);
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    public string AccountName => Account.Name ?? "Unknown";

    public string Character => Account.Character ?? "Unknown";
    public string Login => $"{Account.LastLogin.ToShortDateString()} {Account.LastLogin.ToShortTimeString()}";
    public string LoginRequired => Account.LoginRequired ? "Yes" : "No";
    public string Collected => $"{Account.LastCollection.ToShortDateString()} {Account.LastCollection.ToShortTimeString()}";
    public string CollectionRequired => Account.CollectionRequired ? "Yes" : "No";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays);

    public string VPN => string.Join(',', Account.VPN?.ToArray() ?? Array.Empty<string>());

    private List<AccountVpnViewModel> vpns;
    public List<AccountVpnViewModel> Vpns => vpns;

    public string LaurelCount => Account.GetCurrency("Laurel")?.ToString() ?? "?";
    public string MysticCoinCount => Account.GetCurrency("MysticCoin")?.ToString() ?? "?";

    public int Attempt => Account.Attempt;
    public RunState RunStatus => Account.RunStatus;
    public string? TooltipText => Account.StatusMessage;

    public bool IsSelected { get; set; }
}