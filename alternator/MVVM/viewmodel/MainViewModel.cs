namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class MainViewModel : ObservableObject
{
    public IAsyncCommand? LoginAllMultiCommand { get; set; }
    public IAsyncCommand? LoginAllSingleCommand { get; set; }
    public IAsyncCommand? CollectCommand { get; set; }
    public IAsyncCommand? UpdateCommand { get; set; }
    public IAsyncCommand? StopCommand { get; set; }
    public IAsyncCommand? ShowSettingsCommand { get; set; }

    public string? TimeUtc { get; set; }

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
            running = value;
            LoginAllMultiCommand?.RaiseCanExecuteChanged();
            LoginAllSingleCommand?.RaiseCanExecuteChanged();
            CollectCommand?.RaiseCanExecuteChanged();
            UpdateCommand?.RaiseCanExecuteChanged();
            StopCommand?.RaiseCanExecuteChanged();
            ShowSettingsCommand?.RaiseCanExecuteChanged();
        }
    }

    public MainViewModel()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var datFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat"));

        SetLogging();

        accountManager = new AccountManager(AccountsJson);
        accountManager.Loaded += OnAccountsLoaded;
        AccountsViewModel = new AccountsViewModel();

        Initialise();

        async Task LaunchMultipleAccounts(int maxInstances, LaunchType launchType)
        {
            try
            {
                Running = true;

                cts = new CancellationTokenSource();
                cts.Token.ThrowIfCancellationRequested();
                var launcher = new ClientController(datFile, launchType);

                var accountsToRun = accountManager.AccountsToRun(launchType);
                if (accountsToRun == null) return;
                await launcher.Launch(accountsToRun, maxInstances, cts.Token);

                await accountManager.Save();

                LogManager.Shutdown();
            }
            finally
            {
                Running = false;
            }
        }

        LoginAllMultiCommand = new AsyncCommand(async () => { await LaunchMultipleAccounts(4, LaunchType.LaunchAll); }, _ => !Running);
        LoginAllSingleCommand = new AsyncCommand(async () => { await LaunchMultipleAccounts(1, LaunchType.LaunchNeeded); }, _ => !Running);
        CollectCommand = new AsyncCommand(async () => { await LaunchMultipleAccounts(2, LaunchType.CollectNeeded); }, _ => !Running);
        UpdateCommand = new AsyncCommand(async () => { await LaunchMultipleAccounts(1, LaunchType.UpdateAll); }, _ => !Running);

        StopCommand = new AsyncCommand(async () => cts?.Cancel(), _ => Running);
        ShowSettingsCommand = new AsyncCommand(async () =>
        {

        });
    }


    private void OnAccountsLoaded(object? sender, EventArgs e)
    {
        AccountsViewModel.Add(accountManager.Accounts);
    }

    private void Initialise()
    {
        //Task.Run(() =>
        //{
            accountManager.Load();
       // });

    }

    private void SetLogging()
    {
        var config = new NLog.Config.LoggingConfiguration();

        // Targets where to log to: File and Console
        var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "alternator-log.txt", DeleteOldFileOnStartup = true };
        var debugLogfile = new NLog.Targets.FileTarget("debuglogfile") { FileName = "alternator-debug-log.txt", DeleteOldFileOnStartup = true };
        var errorLogfile = new NLog.Targets.FileTarget("errorlogfile") { FileName = "alternator-error-log.txt" };
        var logConsole = new NLog.Targets.ConsoleTarget("logconsole");

        // Rules for mapping loggers to targets            
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, debugLogfile);
        config.AddRule(LogLevel.Error, LogLevel.Fatal, errorLogfile);
        config.AddRule(LogLevel.Info, LogLevel.Info, logfile);

        // Apply config           
        LogManager.Configuration = config;
    }
}

