namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountsViewModel : ObservableObject
{

    public ObservableCollectionEx<AccountViewModel> Accounts { get; }

    public AccountsViewModel()
    {
        Accounts = new ObservableCollectionEx<AccountViewModel>();
    }

    public void Add(IEnumerable<IAccount>? accounts)
    {
        if (accounts == null) return;
        Accounts.AddRange(accounts.Select(a => new AccountViewModel(a)));
        OnPropertyChanged(nameof(ApiVisibility));
    }

    public void Add(AccountCollection accountCollection)
    {
        Add(accountCollection?.Accounts);
    }

    public IEnumerable<IAccount> SelectedAccounts => Accounts.Where(i => i.IsSelected).Select(i => i.Account);


    public Visibility ApiVisibility => Accounts.Any(a => !string.IsNullOrEmpty(a.Account.ApiKey)) ? Visibility.Visible : Visibility.Hidden;

    public void Clear()
    {
       Accounts.Clear();
    }
}