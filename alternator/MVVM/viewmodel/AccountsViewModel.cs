namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountsViewModel : ObservableCollectionEx<AccountViewModel>
{
    public ListCollectionView? EntriesCollectionView { get; }

    public AccountsViewModel()
    {
        EntriesCollectionView = CollectionViewSource.GetDefaultView(this) as ListCollectionView;
    }

    public void Add(IEnumerable<Account>? accounts)
    {
        if (accounts == null) return;
        AddRange(accounts.Select(a => new AccountViewModel(a)));
    }
}