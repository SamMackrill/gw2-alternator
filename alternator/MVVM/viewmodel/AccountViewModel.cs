namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    public IAccount Account { get;}
    private readonly ISettingsController settingsController;

    public AccountViewModel(IAccount account, ISettingsController settingsController)
    {
        Account = account;
        account.PropertyChanged += ModelPropertyChanged;
        this.settingsController = settingsController;
        this.settingsController.PropertyChanged += SettingsController_PropertyChanged;
    }

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "DisplayLocalTime") return;
        OnPropertyChanged(nameof(Login));
        OnPropertyChanged(nameof(Collected));

    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "Name",           new() { nameof(AccountName) } },
        { "LastLogin",      new() { nameof(Login)       } },
        { "LastCollection", new() { nameof(Collected)   } },
        { "CreatedAt",      new() { nameof(Age)         } },
        { "StatusMessage",  new() { nameof(TooltipText) } },
        { "Vpns",           new() { nameof(VpnsDisplay) } },
        { "LoginCount",     new() { nameof(LoginCount), nameof(MysticCoinCount) } },
    };

    private void ModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }
    private string DateTimeDisplay(DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue) return "Never";
        var displayDateTime = settingsController.Settings is {DisplayLocalTime: true} ? dateTime.ToLocalTime() : dateTime;
        return $"{displayDateTime.ToShortDateString()} {displayDateTime.ToShortTimeString()}";
    }

    public string AccountName => Account.Name ?? "Unknown";

    public string Character => Account.Character ?? "Unknown";
    public string Login => DateTimeDisplay(Account.LastLogin);
    public string LoginRequired => Account.LoginRequired ? "Yes" : "No";
    public string Collected => DateTimeDisplay(Account.LastCollection);

    public string CollectionRequired => Account.CollectionRequired ? "Yes" : "No";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays);

    public string VpnsDisplay => string.Join(',', Account.Vpns?.OrderBy(v => v).ToArray() ?? Array.Empty<string>());

    public List<AccountVpnViewModel> Vpns { get; set; }

    public string LaurelCount => Account.LaurelsGuess.ToString("F0");
    public string MysticCoinCount => Account.MysticCoinsGuess.ToString("F0");

    public int Attempt => Account.Attempt;
    public int LoginCount => Account.LoginCount;
    public RunState RunStatus => Account.RunStatus;
    public string? TooltipText => Account.StatusMessage;

    public bool IsSelected { get; set; }
}