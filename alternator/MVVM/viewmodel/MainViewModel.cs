namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class MainViewModel : ObservableObject
{
    private readonly SettingsController settingsController;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public IAsyncCommand? LoginCommand { get; set; }
    public IAsyncCommand? CollectCommand { get; set; }
    public IAsyncCommand? UpdateCommand { get; set; }
    public ICommandExtended? StopCommand { get; set; }
    public ICommandExtended? ShowSettingsCommand { get; }
    public IAsyncCommand? CloseCommand { get; }

    private CancellationTokenSource? cts;
    private readonly AuthenticationThrottle authenticationThrottle;

    private List<IAccount> accountsToLookup;

    private readonly AccountCollection accountCollection;
    private readonly VpnCollection vpnCollection;
    public AccountsViewModel AccountsVM { get; set; }
    public SettingsViewModel SettingsVM { get; set; }

    private bool running;
    private bool Running
    {
        get => running;
        set
        {
            if (!SetProperty(ref running, value)) return;
            RefreshRunState();
            OnPropertyChanged(nameof(LoginText));
            OnPropertyChanged(nameof(CollectText));
            OnPropertyChanged(nameof(UpdateText));
            OnPropertyChanged(nameof(StopText));
        }
    }

    private void RefreshRunState()
    {
        LoginCommand?.RaiseCanExecuteChanged();
        CollectCommand?.RaiseCanExecuteChanged();
        UpdateCommand?.RaiseCanExecuteChanged();
        StopCommand?.RaiseCanExecuteChanged();
        ShowSettingsCommand?.RaiseCanExecuteChanged();
        CloseCommand?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRun));
    }

    private LaunchType activeLaunchType;
    private LaunchType ActiveLaunchType
    {
        get => activeLaunchType;
        set
        {
            if (!SetProperty(ref activeLaunchType, value)) return;
            OnPropertyChanged(nameof(LoginText));
            OnPropertyChanged(nameof(CollectText));
            OnPropertyChanged(nameof(UpdateText));
            OnPropertyChanged(nameof(StopText));
        }
    }

    public event Action? RequestClose;

    public void RefreshWindow()
    {
        CloseCommand?.RaiseCanExecuteChanged();
    }

    public string TimeUtc => DateTime.UtcNow.ToString("HH:mm");
    public string ResetCountdown => DateTime.UtcNow.AddDays(1).Date.Subtract(DateTime.UtcNow).ToString(@"h'hr 'm'min'");


    public Visibility VpnVisible =>  string.IsNullOrEmpty(authenticationThrottle.Vpn) ? Visibility.Hidden : Visibility.Visible;
    public string? Vpn => authenticationThrottle.Vpn;

    public Visibility ThrottleVisible => authenticationThrottle.FreeIn > 1 ? Visibility.Visible : Visibility.Hidden;
    public string ThrottleDelay => authenticationThrottle.FreeIn.ToString(@"0's'");

    private bool forceSerial;
    public bool ForceSerial
    {
        get => forceSerial;
        set => SetProperty(ref forceSerial, value);
    }

    private bool forceAll;
    public bool ForceAll
    {
        get => forceAll;
        set => SetProperty(ref forceAll, value);
    }

    private bool ignoreVpn;
    public bool IgnoreVpn
    {
        get => ignoreVpn;
        set => SetProperty(ref ignoreVpn, value);
    }

    private bool stopping;
    public bool Stopping
    {
        get => stopping;
        set
        {
            SetProperty(ref stopping, value);
            OnPropertyChanged(nameof(StopText));
            CloseCommand?.RaiseCanExecuteChanged();
            if (!stopping) stopChecked = false;
        }
    }

    public string LoginText => Running && ActiveLaunchType == LaunchType.Login ? "Logging in" : "Login";
    public string CollectText => Running && ActiveLaunchType == LaunchType.Collect ? "Collecting" : "Collect";
    public string UpdateText => Running && ActiveLaunchType == LaunchType.Update ? "Updating" : "Update";
    public string StopText => Running && Stopping ? "Stopping" : "Stop!";

    public string Version
    {
        get
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion ?? "dev";
        }
    }

    private bool loginChecked;
    public bool LoginChecked
    {
        get => loginChecked;
        set
        {
            SetProperty(ref loginChecked, value);
            CloseCommand?.RaiseCanExecuteChanged();
        }
    }

    private bool collectChecked;
    public bool CollectChecked
    {
        get => collectChecked;
        set
        {
            SetProperty(ref collectChecked, value);
            CloseCommand?.RaiseCanExecuteChanged();
        }
    }

    private bool updateChecked;
    public bool UpdateChecked
    {
        get => updateChecked;
        set
        {
            SetProperty(ref updateChecked, value);
            CloseCommand?.RaiseCanExecuteChanged();
        }
    }

    private bool stopChecked;
    public bool StopChecked
    {
        get => stopChecked;
        set
        {
            SetProperty(ref stopChecked, value);
            CloseCommand?.RaiseCanExecuteChanged();
        }
    }

    bool CanRun(LaunchType launchType)
    {
        var canRun = !Running && settingsController.Settings != null && accountCollection.Ready && vpnCollection.Ready;
        Logger.Debug("{0} {1} ? {2}", nameof(CanRun), launchType, canRun);
        return canRun;
    }

    public MainViewModel(DirectoryInfo applicationFolder, string appData, SettingsController settingsController, AccountCollection accountCollection, VpnCollection vpnCollection)
    {
        this.settingsController = settingsController;
        this.accountCollection = accountCollection;
        this.vpnCollection = vpnCollection;

        var apiConnection = new Gw2Sharp.Connection();
        using var apiClient = new Gw2Sharp.Gw2Client(apiConnection);
        var webApiClient = apiClient.WebApi.V2;

        var buildFetch = webApiClient.Build.GetAsync();


        settingsController.DatFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"Local.dat"));
        settingsController.GfxSettingsFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"GFXSettings.Gw2-64.exe.xml"));

        settingsController.DiscoverGw2ExeLocation();

        authenticationThrottle = new AuthenticationThrottle(settingsController.Settings);
        authenticationThrottle.PropertyChanged += ThrottlePropertyChanged;


        SettingsVM = new SettingsViewModel(settingsController, accountCollection, () => Version);

        accountCollection.Loaded += OnAccountsLoaded;
        accountCollection.LoadFailed += OnAccountsLoadFailed;
        vpnCollection.Loaded += OnVpnsLoaded;
        vpnCollection.LoadFailed += OnVpnsLoadFailed;
        AccountsVM = new AccountsViewModel();

        accountsToLookup = new List<IAccount>();

        Initialise();

        async Task LaunchMultipleAccounts(LaunchType launchType, bool all, bool serial, bool ignoreVpn)
        {
            try
            {
                ActiveLaunchType = launchType;
                Running = true;

                var maxInstances = serial ? 1 : launchType switch
                {
                    LaunchType.Login => settingsController.Settings!.MaxLoginInstances,
                    LaunchType.Collect => 2,
                    LaunchType.Update => 1,
                    _ => 1
                };

                cts = new CancellationTokenSource();
                cts.Token.ThrowIfCancellationRequested();

                var launcher = new ClientController(applicationFolder, settingsController, authenticationThrottle, vpnCollection, launchType);
                await launcher.LaunchMultiple(AccountsVM.SelectedAccounts.ToList(), accountCollection, all, ignoreVpn, maxInstances, cts);

                await accountCollection.Save();
            }
            finally
            {
                Running = false;
                Stopping = false;
            }
        }

        AsyncCommand CreateLaunchCommand(LaunchType launchType, Action? tidyUp) =>
            new(async () =>
            {
                await LaunchMultipleAccounts(launchType, ForceAll, ForceSerial, IgnoreVpn);
                tidyUp?.Invoke();
            }, _ => CanRun(launchType));

        LoginCommand = CreateLaunchCommand(LaunchType.Login, () => LoginChecked = false);
        CollectCommand = CreateLaunchCommand(LaunchType.Collect, () => CollectChecked = false);
        UpdateCommand = CreateLaunchCommand(LaunchType.Update, () => UpdateChecked = false);

        StopCommand = new RelayCommand<object>(_ =>
        {
            Stopping = true;
            cts?.Cancel();
        }, _ => Running);

        CloseCommand = new AsyncCommand(async () =>
        {
            await accountCollection.Save();
            RequestClose?.Invoke();
        }, _ => !Running && RequestClose != null);

        ShowSettingsCommand = new RelayCommand<object>(_ =>
        {
            var settingsView = new SettingsWindow
            {
                DataContext = SettingsVM,
                Owner = Application.Current.MainWindow
            };
            settingsView.ShowDialog();
        });

        var dt = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        dt.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(TimeUtc));
            OnPropertyChanged(nameof(ResetCountdown));
            OnPropertyChanged(nameof(ThrottleDelay));
            OnPropertyChanged(nameof(ThrottleVisible));
            apiFetcher = FetchApiData();
        };
        dt.Start();
    }

    Task apiFetcher;

    private void ThrottlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        OnPropertyChanged(args.PropertyName);
        OnPropertyChanged($"{args.PropertyName}Visible");
    }

    private async Task OnVpnsLoadFailed(object? sender, EventArgs e)
    {
        vpnCollection.Ready = true;
        RefreshRunState();
    }

    private async Task OnVpnsLoaded(object? sender, EventArgs e)
    {
        vpnCollection.Ready = true;
        RefreshRunState();
    }

    private async Task OnAccountsLoadFailed(object? sender, EventArgs e)
    {
        accountCollection.Ready = false;
        RefreshRunState();
    }

    private async Task OnAccountsLoaded(object? sender, EventArgs e)
    {
        AccountsVM.Clear();
        AccountsVM.Add(accountCollection);
        accountCollection.Ready = true;
        RefreshRunState();
         if (accountCollection.Accounts != null) accountsToLookup.AddRange(accountCollection.Accounts);
    }

    private async Task FetchApiData()
    {
        if (accountsToLookup == null || !accountsToLookup.Any()) return;

        var accounts = new List<IAccount>(accountsToLookup);
        accountsToLookup = new List<IAccount>();

        var fetchTasks = accounts
            .Where(a => !string.IsNullOrEmpty(a.ApiKey))
            .Select(FetchAccountDetails)
            .ToList();

        await Task.WhenAll(fetchTasks);
    }


    public const int MysticCoinId = 19976;
    public const int LaurelId = 3;

    private async Task FetchAccountDetails(IAccount account)
    {
        Logger.Debug("{0} Fetching details from GW2 API", account.Name);

        var apiConnection = new Gw2Sharp.Connection(account.ApiKey!);
        using var apiClient = new Gw2Sharp.Gw2Client(apiConnection);
        var webApiClient = apiClient.WebApi.V2;

        var accountReturnTask = webApiClient.Account.GetAsync();
        var charactersReturnTask = webApiClient.Characters.AllAsync();
        var walletReturnTask = webApiClient.Account.Wallet.GetAsync();
        var bankReturnTask = webApiClient.Account.Bank.GetAsync();
        var materialsReturnTask = webApiClient.Account.Materials.GetAsync();

        var accountReturn = await accountReturnTask;
        account.CreatedAt = accountReturn.Created.UtcDateTime;
        account.DisplayName = accountReturn.Name;

        var wallet = await walletReturnTask;

        int laurelCount = wallet.FirstOrDefault(c => c is { Id: LaurelId })?.Value ?? 0;

        var characters = await charactersReturnTask;
        var prime = characters.FirstOrDefault();
        int mysticCoinCount = 0;
        if (prime != null)
        {
            account.Character = prime.Name;

            var allSlots = prime.Bags?
                .SelectMany(bag => bag?.Inventory ?? Array.Empty<Gw2Sharp.WebApi.V2.Models.AccountItem>())
                .Where(i => i != null).ToList();

            mysticCoinCount += (allSlots?.Where(i => i is { Id: MysticCoinId }).Sum(i => i!.Count)).GetValueOrDefault(0);
            // Bags of Mystic Coins
            mysticCoinCount += (allSlots?.Where(i => i is { Id: 68332 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 2;
            mysticCoinCount += (allSlots?.Where(i => i is { Id: 68318 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 4;
            mysticCoinCount += (allSlots?.Where(i => i is { Id: 68330 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 6;
            mysticCoinCount += (allSlots?.Where(i => i is { Id: 68333 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 8;

            // Bags of Laurels
            laurelCount += (allSlots?.Where(i => i is { Id: 68314 }).Sum(i => i!.Count)).GetValueOrDefault(0);
            laurelCount += (allSlots?.Where(i => i is { Id: 68339 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 2;
            laurelCount += (allSlots?.Where(i => i is { Id: 68327 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 3;
            laurelCount += (allSlots?.Where(i => i is { Id: 68336 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 4;
            laurelCount += (allSlots?.Where(i => i is { Id: 68328 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 10;
            laurelCount += (allSlots?.Where(i => i is { Id: 68334 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 15;
            laurelCount += (allSlots?.Where(i => i is { Id: 68351 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 20;
            // Chest of Loyalty
            laurelCount += (allSlots?.Where(i => i is { Id: 68326 }).Sum(i => i!.Count)).GetValueOrDefault(0) * 20;
        }

        var bank = await bankReturnTask;
        var mysticCoinInBank = bank.Where(i => i is { Id: MysticCoinId }).Sum(i => i.Count);
        mysticCoinCount += mysticCoinInBank;

        var materials = await materialsReturnTask;
        mysticCoinCount += (materials.FirstOrDefault(m => m is { Id: MysticCoinId })?.Count).GetValueOrDefault(0);

        account.SetCount("MysticCoin", mysticCoinCount);
        account.SetCount("Laurel", laurelCount);

        Logger.Debug("{0} {1} has {2} Laurels and {3} Mystic Coins", account.Name, account.Character, laurelCount, mysticCoinCount);
    }

    private void Initialise()
    {
        //Task.Run(() =>
        //{
#pragma warning disable CS4014
        accountCollection.Load();
        vpnCollection.Load();
#pragma warning restore CS4014
        // });

    }

}

