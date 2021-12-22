namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    private readonly Account account;

    public AccountViewModel(Account account)
    {
        this.account = account;
        account.PropertyChanged += ModelPropertyChanged;
        account.Client.PropertyChanged += ModelPropertyChanged;
    }

    private readonly Dictionary<string, string> propertyConverter = new()
    {
        { "Name", "Account" },
        { "LastLogin", "Login" },
        { "LastCollection", "Collected" },
        { "CreatedAt", "Age" },
    };

    private void ModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        var propertyName = propertyConverter.ContainsKey(args.PropertyName) ? propertyConverter[args.PropertyName] : args.PropertyName;
        OnPropertyChanged(propertyName);
    }

    public string Account => account.Name ?? "Unknown";

    public string Character => account.Character ?? "Unknown";
    public string Login => $"{account.LastLogin.ToShortDateString()} {account.LastLogin.ToShortTimeString()}";
    public LaunchState LaunchStatus => LaunchState.UpToDate;
    public string Collected => $"{account.LastCollection.ToShortDateString()} {account.LastCollection.ToShortTimeString()}";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(account.CreatedAt).TotalDays);

    public State RunStatus => account.Client?.RunStatus ?? State.Unset;
}