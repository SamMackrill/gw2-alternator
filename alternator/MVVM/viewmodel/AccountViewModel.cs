namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountViewModel : ObservableObject
{
    public AccountViewModel(Account account)
    {
        Account = account;
    }

    private Account Account { get; }

    public string Name => Account.Name ?? "Unknown";
    public string Character => Account.Character ?? "Unknown";
    public string Login => $"{Account.LastLogin.ToShortDateString()} {Account.LastLogin.ToShortTimeString()}";
    public string Collected => $"{Account.LastCollection.ToShortDateString()} {Account.LastCollection.ToShortTimeString()}";
    public int Age => (int)Math.Floor(DateTime.UtcNow.Subtract(Account.CreatedAt).TotalDays);

    public string State => "Ready";
}