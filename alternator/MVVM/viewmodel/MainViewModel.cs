﻿namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class MainViewModel : ObservableObject
{
    public IAsyncCommand? LoginCommand { get; set; }
    public IAsyncCommand? CollectCommand { get; set; }
    public IAsyncCommand? UpdateCommand { get; set; }
    public IAsyncCommand? StopCommand { get; set; }
    public IAsyncCommand? ShowSettingsCommand { get; set; }

    private const string AccountsJson = "accounts.json";
    private CancellationTokenSource? cts;

    private readonly AccountManager accountManager;
    public AccountsViewModel AccountsViewModel { get; set; }

    private bool running;
    private bool Running
    {
        get => running;
        set
        {
            if (!SetProperty(ref running, value)) return;
            LoginCommand?.RaiseCanExecuteChanged();
            CollectCommand?.RaiseCanExecuteChanged();
            UpdateCommand?.RaiseCanExecuteChanged();
            StopCommand?.RaiseCanExecuteChanged();
            ShowSettingsCommand?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(LoginText));
            OnPropertyChanged(nameof(CollectText));
            OnPropertyChanged(nameof(UpdateText));
            OnPropertyChanged(nameof(StopText));
        }
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


    public string TimeUtc => DateTime.UtcNow.ToString("HH:mm:ss");
    public string ResetCountdown => DateTime.UtcNow.AddDays(1).Date.Subtract(DateTime.UtcNow).ToString(@"hh\:mm\:ss");

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

    private bool stopping;
    public bool Stopping
    {
        get => stopping;
        set => SetProperty(ref stopping, value);
    }

    public string LoginText => Running && ActiveLaunchType == LaunchType.Login ? "Logging in" : "Login";
    public string CollectText => Running && ActiveLaunchType == LaunchType.Collect ? "Collecting" : "Collect";
    public string UpdateText => Running && ActiveLaunchType == LaunchType.Update ? "Updating" : "Update";
    public string StopText => Running && Stopping ? "Stopping" : "Stop!";

    public string Version => "0.0.1";

    public MainViewModel()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var datFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat"));
        var gfxSettingsFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2\GFXSettings.Gw2-64.exe.xml"));

        accountManager = new AccountManager(AccountsJson);
        accountManager.Loaded += OnAccountsLoaded;
        AccountsViewModel = new AccountsViewModel();

        Initialise();

        async Task LaunchMultipleAccounts(LaunchType launchType, bool all, bool serial)
        {
            try
            {
                ActiveLaunchType = launchType;
                Running = true;

                var maxInstances = serial ? 1 : launchType switch
                {
                    LaunchType.Login => 4,
                    LaunchType.Collect => 2,
                    LaunchType.Update => 1,
                    _ => 1
                };

                cts = new CancellationTokenSource();
                cts.Token.ThrowIfCancellationRequested();
                var launcher = new ClientController(datFile, gfxSettingsFile, launchType);

                var accountsToRun = accountManager.AccountsToRun(launchType, all);
                if (accountsToRun == null || !accountsToRun.Any()) return;
                await launcher.Launch(accountsToRun, maxInstances, cts.Token);

                await accountManager.Save();
                await launcher.Restore();

                LogManager.Shutdown();
            }
            finally
            {
                Running = false;
                Stopping = false;
            }
        }

        LoginCommand = new AsyncCommand(async () => { await LaunchMultipleAccounts(LaunchType.Login, ForceAll, ForceSerial); }, _ => !Running);
        CollectCommand = new AsyncCommand(async () => { await LaunchMultipleAccounts(LaunchType.Collect, ForceAll, ForceSerial); }, _ => !Running);
        UpdateCommand = new AsyncCommand(async () => { await LaunchMultipleAccounts(LaunchType.Update, ForceAll, ForceSerial); }, _ => !Running);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        StopCommand = new AsyncCommand(async () =>
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Stopping = true;
            cts?.Cancel();
        }, _ => Running);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        ShowSettingsCommand = new AsyncCommand(async () =>
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var settingsView = new SettingsWindow
            {
                DataContext = new SettingsViewModel(this),
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
        };
        dt.Start();
    }


    private void OnAccountsLoaded(object? sender, EventArgs e)
    {
        AccountsViewModel.Add(accountManager.Accounts);
    }

    private void Initialise()
    {
        //Task.Run(() =>
        //{
#pragma warning disable CS4014
        accountManager.Load();
#pragma warning restore CS4014
        // });

    }

}

