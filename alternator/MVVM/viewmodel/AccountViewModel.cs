namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    public IAccount Account { get;}

    public AccountViewModel(IAccount account, VpnCollection vpnCollection)
    {
        Account = account;
        account.PropertyChanged += ModelPropertyChanged;
        Vpns = vpnCollection.GetAccountVpns(Account).OrderBy(v => v.Id).ToList();
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "Name",           new() {"AccountName"  } },
        { "LastLogin",      new() { "Login"       } },
        { "LastCollection", new() { "Collected"   } },
        { "CreatedAt",      new() { "Age"         } },
        { "StatusMessage",  new() { "TooltipText" } },
        { "Vpns",           new() { "VpnsDisplay" } },
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

    public string AccountName => Account.Name;

    public string Character => Account.Character ?? "Unknown";
    public string Login => $"{Account.LastLogin.ToShortDateString()} {Account.LastLogin.ToShortTimeString()}";
    public string LoginRequired => Account.LoginRequired ? "Yes" : "No";
    public string Collected => $"{Account.LastCollection.ToShortDateString()} {Account.LastCollection.ToShortTimeString()}";
    public string CollectionRequired => Account.CollectionRequired ? "Yes" : "No";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays);

    public string VpnsDisplay => string.Join(',', Account.Vpns?.OrderBy(v => v).ToArray() ?? Array.Empty<string>());

    public List<AccountVpnViewModel> Vpns { get; }

    public string LaurelCount => Account.GetCurrency("Laurel")?.ToString() ?? "?";
    public string MysticCoinCount => Account.GetCurrency("MysticCoin")?.ToString() ?? "?";

    public int Attempt => Account.Attempt;
    public RunState RunStatus => Account.RunStatus;
    public string? TooltipText => Account.StatusMessage;

    public bool IsSelected { get; set; }
}