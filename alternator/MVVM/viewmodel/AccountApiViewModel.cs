namespace guildwars2.tools.alternator.MVVM.viewmodel;


[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class AccountApiViewModel : ObservableObject
{
    private readonly IAccount account;
    public ICommandExtended? PasteApiKeyCommand { get; }
    public ICommandExtended? UndoApiKeyCommand { get; }

    public string AccountName => account.Name;

    public string? ApiKey
    {
        get => account.ApiKey;
        set => account.ApiKey = value;
    }

    private string? status;
    public string? Status
{
        get => status;
        set => SetProperty(ref status, value);
}

    public AccountApiViewModel(IAccount account)
    {
        this.account = account;
        account.PropertyChanged += Account_PropertyChanged;

        PasteApiKeyCommand = new RelayCommand<object>(_ =>
        {
            var pasteText = Clipboard.GetText();
            if (!account.CheckApiKeyValid(pasteText))
            {
                Status = "Invalid paste";
                return;
            }

            ApiKey = pasteText;
        });

        UndoApiKeyCommand = new RelayCommand<object>(_ =>
        {
            account.Undo();
        });

    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "Name", new() { nameof(AccountName) } },
    };


    private async  void Account_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        var propertyNames = new List<string> { args.PropertyName };
        if (propertyConverter.ContainsKey(args.PropertyName)) propertyNames.AddRange(propertyConverter[args.PropertyName]);
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }

        if (args.PropertyName == "ApiKey")
        {
            Status = "Testing...";
            Status = await account.TestApiKey();
        }
    }

    public bool IsSelected { get; set; }

    private string DebugDisplay => $"{account.Name} {account.ApiKey}";

}