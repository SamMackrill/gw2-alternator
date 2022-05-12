﻿namespace guildwars2.tools.alternator.MVVM.viewmodel;

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
        this.settingsController.PropertyChanged += SettingsController_PropertyChanged; 
        Accounts = new ObservableCollectionEx<AccountViewModel>();
    }

    private void VpnCollection_Updated(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(VpnVisibility));
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "AlwaysIgnoreVpn", new() { nameof(VpnVisibility) } },
    };

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }

    public void Add(IEnumerable<IAccount>? accounts)
    {
        if (accounts == null) return;
        Accounts.AddRange(accounts.Select(a => new AccountViewModel(a, settingsController)));
        OnPropertyChanged(nameof(ApiVisibility));
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

    public Visibility VpnVisibility => (settingsController.Settings?.AlwaysIgnoreVpn ?? true) || !vpnCollection.Any() ? Visibility.Hidden : Visibility.Visible;
    public Visibility ApiVisibility => Accounts.Any(a => !string.IsNullOrEmpty(a.Account.ApiKey)) ? Visibility.Visible : Visibility.Hidden;

    public void SetVpns()
    {
        foreach (var account in Accounts.Where(a => a.Account != null))
        {
            account.Vpns = vpnCollection.GetAccountVpns(account.Account!).OrderBy(v => v.Id).ToList();
        }
    }


    // Totals
    public string DisplayText => "TOTAL";

    public string TotalChests => Accounts.Sum(a => a.Account?.LoginCount ?? 0).ToString();
    public string TotalLaurels => Accounts.Sum(a => a.Account?.LaurelsGuess ?? 0).ToString();
    public string TotalMC => Accounts.Sum(a => a.Account?.MysticCoinsGuess ?? 0).ToString();
}