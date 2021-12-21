namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    private readonly Account account;

    public AccountViewModel(Account account)
    {
        this.account = account;
        account.PropertyChanged += PropertyChanged;
        account.Client.PropertyChanged += PropertyChanged;
    }

    private readonly Dictionary<string, string> propertyConverter = new()
    {
        { "LastLogin", "Login" },
        { "LastCollection", "Collected" },
        { "CreatedAt", "Age" },
    };

    private void PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        var propertyName = propertyConverter.ContainsKey(args.PropertyName) ? propertyConverter[args.PropertyName] : args.PropertyName;
        OnPropertyChanged(propertyName);
    }

    public string Name => account.Name ?? "Unknown";
    public string Character => account.Character ?? "Unknown";
    public string Login => $"{account.LastLogin.ToShortDateString()} {account.LastLogin.ToShortTimeString()}";
    public LaunchState LaunchStatus => LaunchState.UpToDate;
    public string Collected => $"{account.LastCollection.ToShortDateString()} {account.LastCollection.ToShortTimeString()}";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(account.CreatedAt).TotalDays);

    public State RunStatus => account.Client?.RunStatus ?? State.Unset;
}