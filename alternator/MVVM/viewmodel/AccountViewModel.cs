namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    public IAccount? Account { get; }
    private readonly ISettingsController settingsController;

    public AccountViewModel(IAccount account, ISettingsController settingsController)
    {
        Account = account;
        account.PropertyChanged += ModelPropertyChanged;
        this.settingsController = settingsController;
        this.settingsController.PropertyChanged += SettingsController_PropertyChanged;
    }

#pragma warning disable CS8618
    public AccountViewModel()
#pragma warning restore CS8618
    {
    }

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "DisplayLocalTime") return;
        OnPropertyChanged(nameof(Login));
        OnPropertyChanged(nameof(Collected));

    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "Name", new() { nameof(AccountName) } },
        { "LastLogin", new() { nameof(Login) } },
        { "LastCollection", new() { nameof(Collected) } },
        { "CreatedAt", new() { nameof(Age) } },
        { "StatusMessage", new() { nameof(TooltipText) } },
        { "Vpns", new() { nameof(VpnsDisplay) } },
        { "LoginCount", new() { nameof(LoginCount), nameof(MysticCoinCount) } },
    };

    private void ModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }
    private string DateTimeDisplay(DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue) return "Never";
        var displayDateTime = settingsController.Settings is { DisplayLocalTime: true } ? dateTime.ToLocalTime() : dateTime;
        return $"{displayDateTime.ToShortDateString()} {displayDateTime.ToShortTimeString()}";
    }

    public string AccountName => Account == null ? "TOTAL" : Account.Name ?? "Unknown";

    public string Character => Account == null ? "" : Account.Character ?? "Unknown";
    public string Login => Account == null ? "" : DateTimeDisplay(Account.LastLogin);
    public string LoginRequired => Account == null ? "" : Account.LoginRequired ? "Yes" : "No";
    public string Collected => Account == null ? "" : DateTimeDisplay(Account.LastCollection);

    public string CollectionRequired => Account == null ? "" : Account.CollectionRequired ? "Yes" : "No";
    public string Age => Account == null ? "" : Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays).ToString("F0");

    public string VpnsDisplay => Account == null ? "" : string.Join(',', Account.Vpns?.OrderBy(v => v).ToArray() ?? Array.Empty<string>());

    public List<AccountVpnViewModel> Vpns { get; set; }

    private string aggregateLaurelCount;
    public string LaurelCount
    {
        get => Account == null ? aggregateLaurelCount : Account.LaurelsGuess.ToString("F0");
        set => aggregateLaurelCount = value.Trim();
    }

    private string aggregateMysticCoinCount;
    public string MysticCoinCount
    {
        get => Account == null ? aggregateMysticCoinCount : Account.MysticCoinsGuess.ToString("F0");
        set => aggregateMysticCoinCount = value.Trim();
    }

    private string aggregateAttempt;
    public string Attempt
    {
        get => Account == null ? aggregateAttempt : Account.Attempt.ToString();
        set => aggregateAttempt = value.Trim();
    }

    private string aggregateLoginCount;

    public string LoginCount
    {
        get => Account == null ? aggregateLoginCount : Account.LoginCount.ToString();
        set => aggregateLoginCount = value.Trim();
    }

    public string RunStatus => Account == null ? "" : Account.RunStatus.ToString();
    public string? TooltipText => Account == null ? "" : Account.StatusMessage;

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set => _ = SetProperty(ref isSelected, value);
    }
}

