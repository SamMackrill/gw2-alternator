namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountsViewModel : ObservableObject
{
    public SettingsController SettingsController { get; }

    public ObservableCollectionEx<AccountViewModel> Accounts { get; }

    public AccountsViewModel(SettingsController settingsController)
    {
        SettingsController = settingsController;
        SettingsController.PropertyChanged += SettingsController_PropertyChanged; 
        Accounts = new ObservableCollectionEx<AccountViewModel>();
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "AlwaysIgnoreVpn", new() { "VpnVisibility" } },
    };

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        var propertyNames = new List<string> { args.PropertyName };
        if (propertyConverter.ContainsKey(args.PropertyName)) propertyNames.AddRange(propertyConverter[args.PropertyName]);
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }


    public void Add(IEnumerable<IAccount>? accounts, VpnCollection vpnCollection)
    {
        if (accounts == null) return;
        Accounts.AddRange(accounts.Select(a => new AccountViewModel(a, vpnCollection)));
        OnPropertyChanged(nameof(ApiVisibility));
    }

    public void Add(AccountCollection accountCollection, VpnCollection vpnCollection)
    {
        Add(accountCollection?.Accounts, vpnCollection);
    }

    public IEnumerable<IAccount> SelectedAccounts => Accounts.Where(i => i.IsSelected).Select(i => i.Account);

    public void Clear()
    {
       Accounts.Clear();
    }

    public Visibility VpnVisibility => SettingsController.Settings?.AlwaysIgnoreVpn ?? true ? Visibility.Hidden : Visibility.Visible;
    public Visibility ApiVisibility => Accounts.Any(a => !string.IsNullOrEmpty(a.Account.ApiKey)) ? Visibility.Visible : Visibility.Hidden;

}