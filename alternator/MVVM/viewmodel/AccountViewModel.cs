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
        switch (e.PropertyName)
        {
            case "DisplayLocalTime":
                OnPropertyChanged(nameof(Login));
                OnPropertyChanged(nameof(Collected));
                break;
            case "CollectionSpan":
                OnPropertyChanged(nameof(CollectionRequired));
                break;
        }
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "Name", new List<string> { nameof(AccountName) } },
        { "LastLogin", new List<string> { nameof(Login) } },
        { "LastCollection", new List<string> { nameof(Collected) } },
        { "CreatedAt", new List<string> { nameof(Age) } },
        { "StatusMessage", new List<string> { nameof(TooltipText) } },
        { "Vpns", new List<string> { nameof(VpnsDisplay) } },
        { "LoginCount", new List<string> { nameof(LoginCount), nameof(MysticCoinCount) } },
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

    public string Main => Account is {IsMain: true} ? "Yes" : "No";
    public string Character => Account == null ? "" : Account.Character ?? "Unknown";
    public string Login => Account == null ? "" : DateTimeDisplay(Account.LastLogin);
    public string LoginRequired => Account == null ? "" : Account.LoginRequired ? "Yes" : "No";
    public string Collected => Account == null ? "" : DateTimeDisplay(Account.LastCollection);

    public string CollectionRequired => Account == null ? "" : Account.CollectionRequired(settingsController.Settings?.CollectionSpan ?? 30) ? "Yes" : "No";
    public int Age => Account == null ? 0 : (int)Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays);

    public string VpnsDisplay => Account == null ? "" : string.Join(',', Account.Vpns?.OrderBy(v => v).ToArray() ?? Array.Empty<string>());

    public List<AccountVpnViewModel>? Vpns { get; set; }

    private int aggregateLaurelCount;
    public int LaurelCount
    {
        get => Account?.LaurelsGuess ?? aggregateLaurelCount;
        set => aggregateLaurelCount = value;
    }

    private int aggregateMysticCoinCount;
    public int MysticCoinCount
    {
        get => Account?.MysticCoinsGuess ?? aggregateMysticCoinCount;
        set => aggregateMysticCoinCount = value;
    }

    private int aggregateAttempt;
    public int Attempt
    {
        get => Account?.Attempt ?? aggregateAttempt;
        set => aggregateAttempt = value;
    }

    private int aggregateLoginCount;
    public int LoginCount
    {
        get => Account?.LoginCount ?? aggregateLoginCount;
        set => aggregateLoginCount = value;
    }

    public string RunStatus => Account == null ? "" : Account.RunStatus.ToString().SplitCamelCase().Replace("Unset", "");
    public string? TooltipText => Account == null ? "" : Account.StatusMessage;

    private bool isSelected;
    public bool IsSelected
    {
        get => isSelected;
        set => _ = SetProperty(ref isSelected, value);
    }
}
