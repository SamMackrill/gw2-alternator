namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountsViewModel : ObservableCollectionEx<AccountViewModel>
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(object), typeof(AccountsViewModel), new PropertyMetadata(default(object)));

    public void Add(IEnumerable<Account>? accounts)
    {
        if (accounts == null) return;
        AddRange(accounts.Select(a => new AccountViewModel(a)));
    }

    public void Add(AccountCollection accountCollection)
    {
        Add(accountCollection?.Accounts);
    }

    public IEnumerable<Account> SelectedAccounts => Items.Where(i => i.IsSelected).Select(i => i.Account);

}