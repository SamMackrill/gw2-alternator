namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    public IAccount Account { get;}

    public AccountViewModel(IAccount account, IVpnCollection vpnCollection)
    {
        Account = account;
        account.PropertyChanged += ModelPropertyChanged;
        Vpns = vpnCollection.GetAccountVpns(Account).OrderBy(v => v.Id).ToList();
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "Name",           new() { nameof(AccountName) } },
        { "LastLogin",      new() { nameof(Login)       } },
        { "LastCollection", new() { nameof(Collected)   } },
        { "CreatedAt",      new() { nameof(Age)         } },
        { "StatusMessage",  new() { nameof(TooltipText) } },
        { "Vpns",           new() { nameof(VpnsDisplay) } },
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
    private static string DateTimeDisplay(DateTime dateTime) => dateTime == DateTime.MinValue ? "Never" : $"{dateTime.ToShortDateString()} {dateTime.ToShortTimeString()}";

    public string AccountName => Account.Name ?? "Unknown";

    public string Character => Account.Character ?? "Unknown";
    public string Login => DateTimeDisplay(Account.LastLogin);
    public string LoginRequired => Account.LoginRequired ? "Yes" : "No";
    public string Collected => DateTimeDisplay(Account.LastCollection);

    public string CollectionRequired => Account.CollectionRequired ? "Yes" : "No";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays);

    public string VpnsDisplay => string.Join(',', Account.Vpns?.OrderBy(v => v).ToArray() ?? Array.Empty<string>());

    public List<AccountVpnViewModel> Vpns { get; }

    public string LaurelCount => Account.GetCurrency("Laurel")?.ToString() ?? "?";
    public string MysticCoinCount => Account.GetCurrency("MysticCoin")?.ToString() ?? "?";

    public int Attempt => Account.Attempt;
    public int LoginCount => Account.LoginCount;
    public RunState RunStatus => Account.RunStatus;
    public string? TooltipText => Account.StatusMessage;

    public bool IsSelected { get; set; }
}