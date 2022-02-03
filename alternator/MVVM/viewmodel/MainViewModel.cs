using AsyncAwaitBestPractices;

namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class MainViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public IAsyncCommand? LoginCommand { get; set; }
    public IAsyncCommand? CollectCommand { get; set; }
    public IAsyncCommand? UpdateCommand { get; set; }
    public ICommandExtended? StopCommand { get; set; }
    public ICommandExtended? ShowSettingsCommand { get; }
    public ICommandExtended? CopyMetricsCommand { get; }
    public IAsyncCommand? CloseCommand { get; }

    private CancellationTokenSource launchCancellation;
    private CancellationTokenSource apiFetchCancellation;

    private readonly SettingsController settingsController;
    private readonly AuthenticationThrottle authenticationThrottle;
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

    private bool forceSerialOverride;
    public bool ForceSerialOverride
    {
        get => forceSerialOverride;
        set => SetProperty(ref forceSerialOverride, value);
    }

    private bool forceAllOverride;
    public bool ForceAllOverride
    {
        get => forceAllOverride;
        set => SetProperty(ref forceAllOverride, value);
    }


    public Visibility VpnVisibility => settingsController.Settings?.AlwaysIgnoreVpn ?? true ? Visibility.Hidden : Visibility.Visible;

    private bool ignoreVpnOverride;
    public bool IgnoreVpnOverride
    {
        get => ignoreVpnOverride;
        set => SetProperty(ref ignoreVpnOverride, value);
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


    public MainViewModel(
        DirectoryInfo applicationFolder, 
        string appData, 
        SettingsController settingsController,
        AccountCollection accountCollection, 
        VpnCollection vpnCollection)
    {
        this.settingsController = settingsController;
        this.accountCollection = accountCollection;
        this.vpnCollection = vpnCollection;

        settingsController.DatFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"Local.dat"));
        settingsController.GfxSettingsFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"GFXSettings.Gw2-64.exe.xml"));

        settingsController.DiscoverGw2ExeLocation();

        authenticationThrottle = new AuthenticationThrottle(settingsController.Settings);
        authenticationThrottle.PropertyChanged += ThrottlePropertyChanged;


        SettingsVM = new SettingsViewModel(settingsController, accountCollection, () => Version);
        AccountsVM = new AccountsViewModel(settingsController);

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

                launchCancellation = new CancellationTokenSource();
                launchCancellation.Token.ThrowIfCancellationRequested();
                var launcher = new ClientController(applicationFolder, settingsController, authenticationThrottle, vpnCollection, launchType);
                launcher.MetricsUpdated += Launcher_MetricsUpdated;
                await launcher.LaunchMultiple(AccountsVM.SelectedAccounts.ToList(), accountCollection, all, ignoreVpn, maxInstances, launchCancellation);

                await SaveCollections(accountCollection, vpnCollection);
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
                await LaunchMultipleAccounts(launchType, ForceAllOverride, ForceSerialOverride, IgnoreVpnOverride);
                tidyUp?.Invoke();
            }, _ => CanRun(launchType));

        LoginCommand = CreateLaunchCommand(LaunchType.Login, () => LoginChecked = false);
        CollectCommand = CreateLaunchCommand(LaunchType.Collect, () => CollectChecked = false);
        UpdateCommand = CreateLaunchCommand(LaunchType.Update, () => UpdateChecked = false);

        StopCommand = new RelayCommand<object>(_ =>
        {
            Stopping = true;
            launchCancellation?.Cancel();
        }, _ => Running);

        CloseCommand = new AsyncCommand(async () =>
        {
            await SaveCollections(accountCollection, vpnCollection);
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

        CopyMetricsCommand = new RelayCommand<object>(_ =>
        {
            Clipboard.SetText(File.ReadAllText(settingsController.MetricsFile));

        }, _ => MetricsAvailable(settingsController));

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
        };
        dt.Start();
    }

    private void Launcher_MetricsUpdated(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MetricsAvailable));
    }

    private static bool MetricsAvailable(SettingsController settingsController)
    {
        return File.Exists(settingsController.MetricsFile);
    }

    public void Initialise()
    {
        QueryGw2Version().SafeFireAndForget(onException: ex =>
        {
            Logger.Error(ex, "Query GW2 Version");
        });
        LoadAccounts().SafeFireAndForget(onException: ex =>
        {
            Logger.Error(ex, "Load Accounts");
        });
        LoadVpns().SafeFireAndForget(onException: ex =>
        {
            Logger.Error(ex, "Load VPNs");
        });
    }

    private async ValueTask QueryGw2Version()
    {
        var apiConnection = new Gw2Sharp.Connection();
        using var apiClient = new Gw2Sharp.Gw2Client(apiConnection);
        var webApiClient = apiClient.WebApi.V2;

        var build =  await webApiClient.Build.GetAsync();

        // TODO check versions
    }

    private async ValueTask LoadAccounts()
    {

        await accountCollection.Load();

        AccountsVM.Clear();
        AccountsVM.Add(accountCollection, vpnCollection);
        accountCollection.Ready = true;

        RefreshRunState();

        apiFetchCancellation = new CancellationTokenSource();
        await FetchApiData(accountCollection.Accounts, apiFetchCancellation.Token);
    }

    private async ValueTask LoadVpns()
    {
        await vpnCollection.Load();
        vpnCollection.Ready = true;
        RefreshRunState();
    }

    private static async Task SaveCollections(AccountCollection accountCollection, VpnCollection vpnCollection)
    {
        await Task.WhenAll(accountCollection.Save(), vpnCollection.Save());
    }

    private void ThrottlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        OnPropertyChanged(args.PropertyName);
        OnPropertyChanged($"{args.PropertyName}Visible");
    }

    private async Task FetchApiData(List<Account>? accounts, CancellationToken cancellationToken)
    {
        if (accounts==null) return;

        var options = new ParallelOptions { MaxDegreeOfParallelism = 3 };
        await Parallel.ForEachAsync(accounts.Where(a => !string.IsNullOrEmpty(a.ApiKey)), options, async (account, _) => await account.FetchAccountDetails(cancellationToken));
    }

}