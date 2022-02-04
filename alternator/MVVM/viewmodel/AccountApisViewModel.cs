namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountApisViewModel : ObservableObject
{

    public ObservableCollectionEx<AccountApiViewModel> AccountApis { get; }

    public AccountApisViewModel()
    {
        AccountApis = new ObservableCollectionEx<AccountApiViewModel>();
    }

    public void Add(IEnumerable<IAccount>? accounts)
    {
        if (accounts == null) return;
        AccountApis.AddRange(accounts.Select(a => new AccountApiViewModel(a)));
    }

}