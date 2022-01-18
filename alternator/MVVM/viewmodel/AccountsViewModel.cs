namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountsViewModel : ObservableCollectionEx<AccountViewModel>
{
    public void Add(IEnumerable<IAccount>? accounts)
    {
        if (accounts == null) return;
        AddRange(accounts.Select(a => new AccountViewModel(a)));
    }

    public void Add(AccountCollection accountCollection)
    {
        Add(accountCollection?.Accounts);
    }

    public IEnumerable<IAccount> SelectedAccounts => Items.Where(i => i.IsSelected).Select(i => i.Account);

}