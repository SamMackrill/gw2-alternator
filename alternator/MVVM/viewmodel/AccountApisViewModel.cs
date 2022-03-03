namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class AccountApisViewModel : ObservableObject
{
    private readonly ISettingsController settingsController;

    public ObservableCollectionEx<AccountApiViewModel> AccountApis { get; }

    public AccountApisViewModel(ISettingsController settingsController)
    {
        this.settingsController = settingsController;
        AccountApis = new ObservableCollectionEx<AccountApiViewModel>();

        settingsController.PropertyChanged += SettingsController_PropertyChanged;
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "FontSize", new() { "HeaderFontSize" } },
    };

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }

    public double FontSize => settingsController.Settings?.FontSize ?? SettingsController.DefaultSettings.FontSize;
    public double HeaderFontSize => settingsController.Settings?.HeaderFontSize ?? SettingsController.DefaultSettings.FontSize;

    public void Add(IEnumerable<IAccount>? accounts)
    {
        if (accounts == null) return;
        AccountApis.AddRange(accounts.Select(a => new AccountApiViewModel(a)));
    }

}