
namespace guildwars2.tools.alternator.MVVM.viewmodel;

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
    public IRelayCommand? ShowLogFileCommand { get; }
    
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

    public static string Version
    {
        get
        {
            var mainModule = Process.GetCurrentProcess().MainModule;
            if (mainModule == null) return "?.?.?";

            return $"{mainModule.FileVersionInfo.FileMajorPart}.{mainModule.FileVersionInfo.FileMinorPart}.{mainModule.FileVersionInfo.FileBuildPart}" ;
        }
    }

    public double FontSize => settingsController.Settings?.FontSize ?? SettingsController.DefaultSettings.FontSize;
    public double HeaderFontSize => settingsController.Settings?.HeaderFontSize ?? SettingsController.DefaultSettings.FontSize;

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
        return !Running && settingsController.Settings != null && accountCollection.Ready;
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "AlwaysIgnoreVpn", new() { nameof(VpnVisibility) } },
        { "FontSize", new() { nameof(HeaderFontSize) } },
    };

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }


    public DelegateLoadedAction LoadAction { get; }

    public RelayCommand<object> SelectAllCommand => new(_ =>
    {
        AccountsVM.SelectAll();
    });


    public MainViewModel(IDialogService dialogService)
    {

        LoadAction = new DelegateLoadedAction(() => {
            Logger.Debug("Main Window Loaded");
        });

        settingsController = Ioc.Default.GetRequiredService<ISettingsController>();
        settingsController.PropertyChanged += SettingsController_PropertyChanged;
        settingsController.Load();

        accountCollection = Ioc.Default.GetRequiredService<IAccountCollection>();
        accountCollection.Loaded += AccountCollection_Loaded;

        vpnCollection = Ioc.Default.GetRequiredService<IVpnCollection>();
        vpnCollection.Loaded += VpnCollection_Loaded;

        SettingsVM = new SettingsViewModel(settingsController, accountCollection, () => Version, dialogService);
        AccountsVM = new AccountsViewModel(settingsController, vpnCollection);
        AccountApisVM = new AccountApisViewModel(settingsController);
        VpnConnectionsVM = new VpnConnectionsViewModel(vpnCollection, settingsController);

        authenticationThrottle = new AuthenticationThrottle(settingsController.Settings);
        authenticationThrottle.PropertyChanged += ThrottlePropertyChanged;

        ForceSerialOverride = true;


        QueryGw2Version().SafeFireAndForget(ex =>
        {
            Logger.Error(ex, "Query GW2 Version");
        });

        LoadAccountsAndVpns().SafeFireAndForget(ex =>
        {
            Logger.Error(ex, "Load Accounts");
            if (ex is Gw2NoAccountsException)
            {
                Logger.Error(ex, "Load Accounts");
                Application.Current.Dispatcher.Invoke(() => {
                    Application.Current.MainWindow?.Show();
                    var showerVM = new MessageShowerViewModel();
                    var showerView = new MessageShowerView { DataContext = showerVM };
                    showerView.Show();
                    _ = dialogService.ShowMessageBox(
                        showerVM,
                        "No accounts defined, please import via settings",
                        "GW2-Alternator",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);
                    ShowSettings();
                });
            }

        });

        async Task LaunchMultipleAccounts(
            LaunchType launchType, 
            bool all,
            bool serial,
            bool logAccounts,
            bool ignoreVpn)
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

                var vpnAccountCount = launchType == LaunchType.Update ? accountCollection.Accounts!.Count : settingsController.Settings!.VpnAccountCount;

                launchCancellation = new CancellationTokenSource();
                launchCancellation.Token.ThrowIfCancellationRequested();
                var launcher = new ClientController(settingsController.ApplicationFolder, settingsController, authenticationThrottle, vpnCollection, launchType);
                launcher.MetricsUpdated += Launcher_MetricsUpdated;
                var selectedAccounts = AccountsVM.SelectedAccounts.ToList();
                await launcher.LaunchMultiple(
                    selectedAccounts, 
                    accountCollection, 
                    all, 
                    !serial,
                    logAccounts,
                    ignoreVpn, 
                    maxInstances,
                    vpnAccountCount,
                    launchCancellation);

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
                await LaunchMultipleAccounts(
                    launchType, 
                    ForceAllOverride, 
                    ForceSerialOverride,
                    settingsController.Settings!.LogAccounts,
                    IgnoreVpnOverride || settingsController.Settings!.AlwaysIgnoreVpn
                    );
                tidyUp?.Invoke();
            }, () => CanRun(launchType));

        LoginCommand = CreateLaunchCommand(LaunchType.Login, () => LoginChecked = false);
        CollectCommand = CreateLaunchCommand(LaunchType.Collect, () => CollectChecked = false);
        UpdateCommand = CreateLaunchCommand(LaunchType.Update, () => UpdateChecked = false);

        StopCommand = new RelayCommand<object>(_ =>
        {
            Logger.Debug("Stop Requested by user");
            Stopping = true;
            launchCancellation?.Cancel("Stop Requested");
        }, _ => Running);

        CloseCommand = new AsyncRelayCommand<Window>(async w =>
        {
            Logger.Debug("Close Requested by user");
            await SaveCollections(accountCollection, vpnCollection);
            w?.Close();
            Application.Current.Shutdown(0);
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

        ShowLogFileCommand = new RelayCommand<object>(_ =>
        {
            var logfile = Path.Combine(App.ApplicationFolder, "gw2-alternator-debug-log.txt");
            try
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        UseShellExecute = true,
                        FileName = logfile
                    }
                };
                process.Start();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error showing logfile {0}", logfile);
            }
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

        if (!Debugger.IsAttached)
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
        Application.Current.MainWindow?.Show();
        var settingsWindow = new SettingsWindow
        {
            DataContext = SettingsVM,
            Owner = Application.Current.MainWindow
        };
        settingsWindow.ShowDialog();
        RefreshRunState();
    }


    public static string Gw2ClientBuild { get; private set; }


    public RelayCommand<object> ResetThrottle => new(_ =>
    {
        authenticationThrottle.Reset();
    });

    private async ValueTask QueryGw2Version()
    {
        var apiConnection = new Gw2Sharp.Connection();
        using var apiClient = new Gw2Sharp.Gw2Client(apiConnection);
        var webApiClient = apiClient.WebApi.V2;

        var build = await webApiClient.Build.GetAsync();

        Gw2ClientBuild =  build.Id.ToString("#,#");

        // TODO check versions (can't as API is not updated)
    }

    private async ValueTask LoadAccountsAndVpns()
    {
        Logger.Debug("Load Accounts");
        var vpnLoadTask = vpnCollection.Load();
        var accountLoadTask = accountCollection.Load();
        await Task.WhenAll(vpnLoadTask, accountLoadTask);
        AccountsVM.SetVpns();

        if (!accountCollection.Any()) throw new Gw2NoAccountsException("No Accounts");
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

        var options = new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken };
        var delay = new TimeSpan(0, 0, 0, 0, 200);
        await Parallel.ForEachAsync(accounts.Where(a => !string.IsNullOrEmpty(a.ApiKey)), options, async (account, _) => await account.FetchAccountDetails(delay, cancellationToken));
    }

}