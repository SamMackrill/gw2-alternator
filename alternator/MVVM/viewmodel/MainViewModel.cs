﻿namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class MainViewModel : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public IAsyncRelayCommand? LoginCommand { get; set; }
    public IAsyncRelayCommand? CollectCommand { get; set; }
    public IAsyncRelayCommand? UpdateCommand { get; set; }
    public IAsyncRelayCommand<Window>? CloseCommand { get; }

    public IRelayCommand? StopCommand { get; set; }
    public IRelayCommand? ShowSettingsCommand { get; }
    public IRelayCommand? ShowApisCommand { get; }
    public IRelayCommand? ShowVpnsCommand { get; }
    public IRelayCommand? CopyMetricsCommand { get; }

    private CancellationTokenSource? launchCancellation;
    private CancellationTokenSource? apiFetchCancellation;

    private readonly AuthenticationThrottle authenticationThrottle;

    private readonly ISettingsController settingsController;
    private readonly IAccountCollection accountCollection;
    private readonly IVpnCollection vpnCollection;

    public AccountsViewModel AccountsVM { get; set; }
    public SettingsViewModel SettingsVM { get; set; }
    public AccountApisViewModel AccountApisVM { get; set; }
    public VpnConnectionsViewModel VpnConnectionsVM { get; set; }

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
        LoginCommand?.NotifyCanExecuteChanged();
        CollectCommand?.NotifyCanExecuteChanged();
        UpdateCommand?.NotifyCanExecuteChanged();
        StopCommand?.NotifyCanExecuteChanged();
        ShowSettingsCommand?.NotifyCanExecuteChanged();
        ShowApisCommand?.NotifyCanExecuteChanged();
        ShowVpnsCommand?.NotifyCanExecuteChanged();
        CloseCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(VpnVisibility));
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

    public string TimeUtc => DateTime.UtcNow.ToString("HH:mm");
    public string ResetCountdown => DateTime.UtcNow.AddDays(1).Date.Subtract(DateTime.UtcNow).ToString(@"h'hr 'm'min'");


    public Visibility CurrentVpnVisible =>  string.IsNullOrEmpty(authenticationThrottle.Vpn) ? Visibility.Collapsed : Visibility.Visible;
    public string? Vpn => authenticationThrottle.Vpn;

    public Visibility ThrottleVisible => authenticationThrottle.FreeIn > 1 ? Visibility.Visible : Visibility.Collapsed;
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


    public Visibility VpnVisibility => (settingsController.Settings?.AlwaysIgnoreVpn ?? true) || !vpnCollection.Any() ? Visibility.Collapsed : Visibility.Visible;
    public Visibility VpnButtonVisibility => settingsController.Settings?.AlwaysIgnoreVpn ?? true ? Visibility.Collapsed : Visibility.Visible;

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
            CloseCommand?.NotifyCanExecuteChanged();
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
            var mainModule = Process.GetCurrentProcess().MainModule;
            if (mainModule == null) return "?.?.?";

            return $"{mainModule.FileVersionInfo.FileMajorPart}.{mainModule.FileVersionInfo.FileMinorPart}.{mainModule.FileVersionInfo.FileBuildPart}" ;
        }
    }

    private bool loginChecked;
    public bool LoginChecked
    {
        get => loginChecked;
        set
        {
            SetProperty(ref loginChecked, value);
            CloseCommand?.NotifyCanExecuteChanged();
        }
    }

    private bool collectChecked;
    public bool CollectChecked
    {
        get => collectChecked;
        set
        {
            SetProperty(ref collectChecked, value);
            CloseCommand?.NotifyCanExecuteChanged();
        }
    }

    private bool updateChecked;
    public bool UpdateChecked
    {
        get => updateChecked;
        set
        {
            SetProperty(ref updateChecked, value);
            CloseCommand?.NotifyCanExecuteChanged();
        }
    }

    private bool stopChecked;
    public bool StopChecked
    {
        get => stopChecked;
        set
        {
            SetProperty(ref stopChecked, value);
            CloseCommand?.NotifyCanExecuteChanged();
        }
    }

    private bool CanRun(LaunchType launchType)
    {
        var canRun = !Running && settingsController.Settings != null && accountCollection.Ready && vpnCollection.Ready;
        //Logger.Debug("{0} {1} ? {2}", nameof(CanRun), launchType, canRun);
        return canRun;
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "AlwaysIgnoreVpn", new() { "VpnVisibility" } },
    };

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        var propertyNames = new List<string> { args.PropertyName };
        if (propertyConverter.ContainsKey(args.PropertyName)) propertyNames.AddRange(propertyConverter[args.PropertyName]);
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    public MainViewModel(IDialogService dialogService)
    {
        settingsController = Ioc.Default.GetRequiredService<ISettingsController>();
        settingsController.PropertyChanged += SettingsController_PropertyChanged;
        settingsController.Load();

        accountCollection = Ioc.Default.GetRequiredService<IAccountCollection>();
        accountCollection.Loaded += AccountCollection_Loaded;

        vpnCollection = Ioc.Default.GetRequiredService<IVpnCollection>();
        vpnCollection.Loaded += VpnCollection_Loaded;

        SettingsVM = new SettingsViewModel(settingsController, accountCollection, () => Version);
        AccountsVM = new AccountsViewModel(settingsController, vpnCollection);
        AccountApisVM = new AccountApisViewModel();
        VpnConnectionsVM = new VpnConnectionsViewModel(vpnCollection, settingsController);

        authenticationThrottle = new AuthenticationThrottle(settingsController.Settings);
        authenticationThrottle.PropertyChanged += ThrottlePropertyChanged;


        QueryGw2Version().SafeFireAndForget(onException: ex =>
        {
            Logger.Error(ex, "Query GW2 Version");
        });
        LoadAccounts().SafeFireAndForget(onException: ex =>
        {
            Logger.Error(ex, "Load Accounts");
            if (ex is Gw2Exception)
            {
                _ = dialogService.ShowMessageBox(
                    this,
                    "No accounts defined, please import via settings",
                    "GW2-Alternator",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);

                Application.Current.Dispatcher.Invoke(ShowSettings);
            }

        });
        LoadVpns().SafeFireAndForget(onException: ex =>
        {
            Logger.Error(ex, "Load VPNs");
        });

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
                var launcher = new ClientController(settingsController.ApplicationFolder, settingsController, authenticationThrottle, vpnCollection, launchType);
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

        AsyncRelayCommand CreateLaunchCommand(LaunchType launchType, Action? tidyUp) =>
            new(async () =>
            {
                await LaunchMultipleAccounts(launchType, ForceAllOverride, ForceSerialOverride, IgnoreVpnOverride || settingsController.Settings!.AlwaysIgnoreVpn);
                tidyUp?.Invoke();
            }, () => CanRun(launchType));

        LoginCommand = CreateLaunchCommand(LaunchType.Login, () => LoginChecked = false);
        CollectCommand = CreateLaunchCommand(LaunchType.Collect, () => CollectChecked = false);
        UpdateCommand = CreateLaunchCommand(LaunchType.Update, () => UpdateChecked = false);

        StopCommand = new RelayCommand<object>(_ =>
        {
            Stopping = true;
            launchCancellation?.Cancel();
        }, _ => Running);

        CloseCommand = new AsyncRelayCommand<Window>(async w =>
        {
            await SaveCollections(accountCollection, vpnCollection);
            w?.Close();
        }, _ => !Running);

        ShowSettingsCommand = new RelayCommand<object>(_ => ShowSettings());

        ShowApisCommand = new RelayCommand<object>(_ =>
        {
            var window = new Gw2AccountApiWindow()
            {
                DataContext = AccountApisVM,
                Owner = Application.Current.MainWindow
            };
            accountCollection.SetUndo();
            window.ShowDialog();
        });

        ShowVpnsCommand = new RelayCommand<object>(_ =>
        {
            VpnConnectionsVM.LookupConnections();
            var window = new VpnsWindow()
            {
                DataContext = VpnConnectionsVM,
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
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

    private void VpnCollection_Loaded(object? sender, EventArgs e)
    {
        Logger.Debug("VPNs Loaded");
        VpnConnectionsVM.Update();
        vpnCollection.Ready = true;
        RefreshRunState();
    }

    private async void AccountCollection_Loaded(object? sender, EventArgs e)
    {
        Logger.Debug("Accounts Loaded");
        AccountsVM.Clear();
        AccountsVM.Add(accountCollection);
        AccountApisVM.Add(accountCollection.Accounts);

        accountCollection.Ready = true;

        RefreshRunState();

        if (true || !Debugger.IsAttached)
        {
            apiFetchCancellation = new CancellationTokenSource();
            await FetchApiData(accountCollection.Accounts, apiFetchCancellation.Token);
        }
    }

    private void Launcher_MetricsUpdated(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MetricsAvailable));
    }

    private static bool MetricsAvailable(ISettingsController settingsController)
    {
        return File.Exists(settingsController.MetricsFile);
    }

    private void ShowSettings()
    {
        var window = new SettingsWindow
        {
            DataContext = SettingsVM,
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
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
        Logger.Debug("Load Accounts");
        await accountCollection.Load();
        if (!accountCollection.Any()) throw new Gw2Exception("No Accounts");
    }

    private async ValueTask LoadVpns()
    {
        Logger.Debug("Load VPNs");
        await vpnCollection.Load();
        //var current = vpnCollection.CurrentLive;
    }

    private static async Task SaveCollections(IAccountCollection accountCollection, IVpnCollection vpnCollection)
    {
        await Task.WhenAll(accountCollection.Save(), vpnCollection.Save());
    }

    private void ThrottlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        OnPropertyChanged(args.PropertyName);
        OnPropertyChanged($"{args.PropertyName}Visible");
    }

    private async Task FetchApiData(List<IAccount>? accounts, CancellationToken cancellationToken)
    {
        if (accounts==null) return;

        var options = new ParallelOptions { MaxDegreeOfParallelism = 3 };
        await Parallel.ForEachAsync(accounts.Where(a => !string.IsNullOrEmpty(a.ApiKey)), options, async (account, _) => await account.FetchAccountDetails(cancellationToken));
    }

}