namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    public Account Account { get;}

    public AccountViewModel(Account account)
    {
        this.Account = account;
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

    public string AccountName => Account.Name ?? "Unknown";

    public string Character => Account.Character ?? "Unknown";
    public string Login => $"{Account.LastLogin.ToShortDateString()} {Account.LastLogin.ToShortTimeString()}";
    public string LoginRequired => Account.LoginRequired ? "Yes" : "No";
    public string Collected => $"{Account.LastCollection.ToShortDateString()} {Account.LastCollection.ToShortTimeString()}";
    public string CollectionRequired => Account.CollectionRequired ? "Yes" : "No";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays);

    public RunState RunStatus => Account.Client?.RunStatus ?? RunState.Unset;

    public string? TooltipText => Account.Client?.StatusMessage;

    public bool IsSelected { get; set; }
}