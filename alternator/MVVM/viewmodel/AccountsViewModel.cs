using Account = Gw2Sharp.WebApi.V2.Models.Account;

namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountsViewModel : ObservableObject
{
    private readonly IVpnCollection vpnCollection;
    private readonly ISettingsController settingsController;

    public ObservableCollectionEx<AccountViewModel> Accounts { get; }

    public AccountsViewModel(ISettingsController settingsController, IVpnCollection vpnCollection)
    {
        this.vpnCollection = vpnCollection;
        vpnCollection.Updated += VpnCollection_Updated;
        this.settingsController = settingsController;
        this.settingsController.PropertyChanged += Dependency_PropertyChanged; 
        Accounts = new ObservableCollectionEx<AccountViewModel>();
    }

    private void VpnCollection_Updated(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(VpnVisibilityHide));
        OnPropertyChanged(nameof(VpnVisibilityCollapse));
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "AlwaysIgnoreVpn", new() { nameof(VpnVisibilityHide), nameof(VpnVisibilityCollapse) } },
        { "LoginRequired", new() { nameof(OutstandingLogins) } },
        { "CollectionRequired", new() { nameof(OutstandingCollections) } },
        { "LaurelCount", new() { nameof(TotalLaurels) } },
        { "MysticCoinCount", new() { nameof(TotalMC) } },
        { "LoginCount", new() { nameof(LoginsSinceCollected) } },
        { "Attempt", new() { nameof(TotalAttempts) } },
    };

    private void Add(IEnumerable<IAccount>? accounts)
    {
        if (accounts == null) return;
        Accounts.AddRange(accounts.Select(a => new AccountViewModel(a, settingsController)));
        OnPropertyChanged(nameof(ApiVisibilityHide));
        OnPropertyChanged(nameof(ApiVisibilityCollapse));

        foreach (var account in Accounts.Where(a => a.Account != null))
        {
            account.PropertyChanged += Dependency_PropertyChanged;
        }
    }

    private void Dependency_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }

    public void Add(IAccountCollection accountCollection)
    {
        Add(accountCollection.Accounts);
    }

    public IEnumerable<IAccount> SelectedAccounts => Accounts.Where(i => i is {IsSelected: true}).Select(i => i.Account)!;

    public void Clear()
    {
       Accounts.Clear();
    }

    public Visibility VpnVisibilityHide => (settingsController.Settings?.AlwaysIgnoreVpn ?? true) || !vpnCollection.Any() ? Visibility.Hidden : Visibility.Visible;
    public Visibility ApiVisibilityHide => Accounts.Any(a => !string.IsNullOrEmpty(a.Account?.ApiKey)) ? Visibility.Visible : Visibility.Hidden;
    public Visibility VpnVisibilityCollapse => (settingsController.Settings?.AlwaysIgnoreVpn ?? true) || !vpnCollection.Any() ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ApiVisibilityCollapse => Accounts.Any(a => !string.IsNullOrEmpty(a.Account?.ApiKey)) ? Visibility.Visible : Visibility.Collapsed;

    public void SetVpns()
    {
        foreach (var account in Accounts.Where(a => a.Account != null))
        {
            account.Vpns = vpnCollection.GetAccountVpns(account.Account!).OrderBy(v => v.Id).ToList();
        }
    }

    public RelayCommand<object> SelectAllCommand => new(_ => { SelectAll(); });

    public void SelectAll()
    {
        var toState = Accounts.Any(a => !a.IsSelected);
        foreach (var account in Accounts)
        {
            account.IsSelected = toState;
        }
    }

    // Totals
    public string DisplayText => "TOTAL";

    public string OutstandingLogins => Accounts.Sum(a => a.Account?.LoginRequired ?? false ? 1 : 0).ToString();
    public string OutstandingCollections => Accounts.Sum(a => a.Account?.CollectionRequired ?? false ? 1 : 0).ToString();

    public string LoginsSinceCollected => Accounts.Sum(a => a.Account?.LoginCount ?? 0).ToString();
    public string TotalLaurels => Accounts.Sum(a => a.Account?.LaurelsGuess ?? 0).ToString();
    public string TotalMC => Accounts.Sum(a => a.Account?.MysticCoinsGuess ?? 0).ToString();

    public string TotalAttempts => Accounts.Sum(a => a.Account?.Attempt ?? 0).ToString();
}