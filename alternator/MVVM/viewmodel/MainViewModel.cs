using System.Net.Http;
using System.Xml.Linq;
using GW2SDK;
using GW2SDK.Builds;
using GW2SDK.Characters;
using GW2SDK.Accounts;
using GW2SDK.Http;
using GW2SDK.Json;
using Gw2Sharp.WebApi.V2;
using Gw2Sharp.WebApi.V2.Models;

namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class MainViewModel : ObservableObject
{
    public IAsyncCommand? LoginCommand { get; set; }
    public IAsyncCommand? CollectCommand { get; set; }
    public IAsyncCommand? UpdateCommand { get; set; }
    public ICommandExtended? StopCommand { get; set; }
    public ICommandExtended? ShowSettingsCommand { get; }
    public IAsyncCommand? CloseCommand { get; }

    private CancellationTokenSource? cts;

    private readonly AccountCollection accountCollection;
    public AccountsViewModel AccountsVM { get; set; }
    public SettingsViewModel SettingsVM { get; set; }

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
            CloseCommand?.RaiseCanExecuteChanged();
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

    public event Action? RequestClose;

    public void RefreshWindow()
    {
        CloseCommand?.RaiseCanExecuteChanged();
    }


    public string TimeUtc => DateTime.UtcNow.ToString("HH:mm");
    public string ResetCountdown => DateTime.UtcNow.AddDays(1).Date.Subtract(DateTime.UtcNow).ToString(@"h'hr 'm'min'");

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


    public MainViewModel(DirectoryInfo applicationFolder, string appData, Settings settings)
    {
        var datFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"Local.dat"));
        var gfxSettingsFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"GFXSettings.Gw2-64.exe.xml"));

        DiscoverGw2ExeLocation(settings, gfxSettingsFile);

        accountCollection = new AccountCollection(applicationFolder, Path.Combine(appData, @"Gw2 Launchbuddy"), Path.Combine(appData, @"Gw2Launcher"));
        SettingsVM = new SettingsViewModel(settings, accountCollection , () => Version);

        accountCollection.Loaded += OnAccountsLoaded;
        AccountsVM = new AccountsViewModel();

        Initialise();

        async Task LaunchMultipleAccounts(LaunchType launchType, bool all, bool serial)
        {
            try
            {
                ActiveLaunchType = launchType;
                Running = true;

                var maxInstances = serial ? 1 : launchType switch
                {
                    LaunchType.Login => settings.MaxLoginInstances,
                    LaunchType.Collect => 2,
                    LaunchType.Update => 1,
                    _ => 1
                };

                cts = new CancellationTokenSource();
                cts.Token.ThrowIfCancellationRequested();
                var launcher = new ClientController(applicationFolder, settings, datFile, gfxSettingsFile, launchType);

                var selectedAccounts = AccountsVM.SelectedAccounts.ToList();

                var accountsToRun = selectedAccounts.Any() ? selectedAccounts : accountCollection.AccountsToRun(launchType, all);

                if (accountsToRun == null || !accountsToRun.Any()) return;
                //accountsToRun = accountsToRun.Where(a => a.Name == "Fish2").ToList();
                await launcher.LaunchMultiple(accountsToRun, maxInstances, cts);

                await accountCollection.Save();
            }
            finally
            {
                Running = false;
                Stopping = false;
            }
        }

        LoginCommand = new AsyncCommand(async () =>
        {
            await LaunchMultipleAccounts(LaunchType.Login, ForceAll, ForceSerial);
            LoginChecked = false;
        }, _ => !Running);
        CollectCommand = new AsyncCommand(async () =>
        {
            await LaunchMultipleAccounts(LaunchType.Collect, ForceAll, ForceSerial);
            CollectChecked = false;
        }, _ => !Running);
        UpdateCommand = new AsyncCommand(async () =>
        {
            await LaunchMultipleAccounts(LaunchType.Update, ForceAll, ForceSerial);
            UpdateChecked = false;
        }, _ => !Running);

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
        };
        dt.Start();
    }

    private static void DiscoverGw2ExeLocation(Settings settings, FileInfo gfxSettingsFile)
    {
        if (!gfxSettingsFile.Exists || Directory.Exists(settings.Gw2Folder)) return;

        var doc = XDocument.Load(gfxSettingsFile.FullName);
        var installPath = doc.Descendants("INSTALLPATH").FirstOrDefault();

        var valueAttribute = installPath?.Attribute("Value");
        if (valueAttribute != null && Directory.Exists(valueAttribute.Value)) settings.Gw2Folder = valueAttribute.Value;
    }


    private void OnAccountsLoaded(object? sender, EventArgs e)
    {
        AccountsVM.Clear();
        AccountsVM.Add(accountCollection);
        _ = FetchApiData(accountCollection.Accounts);
    }

    private async Task FetchApiData(List<model.Account>? accounts)
    {
        if (accounts == null) return;

        var apiConnection = new Gw2Sharp.Connection();
        using var apiClient = new Gw2Sharp.Gw2Client(apiConnection);
        var webApiClient = apiClient.WebApi.V2;

        var build = await webApiClient.Build.GetAsync();

        var fetchTasks = accounts
            .Where(a => !string.IsNullOrEmpty(a.ApiKey))
            .Select(FetchAccountDetails)
            .ToList();

        await Task.WhenAll(fetchTasks);
    }
    private async Task FetchAccountDetails(model.Account account)
    {
        try
        {
            var apiConnection = new Gw2Sharp.Connection(account.ApiKey!);
            using var apiClient = new Gw2Sharp.Gw2Client(apiConnection);
            var webApiClient = apiClient.WebApi.V2;

            var accountReturnTask = webApiClient.Account.GetAsync();
            var charactersReturnTask = webApiClient.Characters.AllAsync();
            var walletReturnTask = webApiClient.Account.Wallet.GetAsync();

            var accountReturn = await accountReturnTask;
            account.CreatedAt = accountReturn.Created.UtcDateTime;
            account.DisplayName = accountReturn.Name;

            var wallet = await walletReturnTask;

            var laurelCount = wallet.FirstOrDefault(c => c.Id == LaurelId)?.Value;

            var characters = await charactersReturnTask;
            var prime = characters.FirstOrDefault();
            int mysticCoinCount = 0;
            if (prime != null)
            {
                account.Character = prime.Name;

                var allSlots = prime.Bags?
                    .SelectMany(bag => bag?.Inventory ?? Array.Empty<AccountItem>())
                    .Where(i => i != null).ToList();

                mysticCoinCount += (allSlots?.Where(i => i!.Id == MysticCoinId).Sum(i => i!.Count)).GetValueOrDefault(0);
                // Bags of Mystic Coins
                mysticCoinCount += (allSlots?.Where(i => i!.Id == 68332).Sum(i => i!.Count)).GetValueOrDefault(0) * 2;
                mysticCoinCount += (allSlots?.Where(i => i!.Id == 68318).Sum(i => i!.Count)).GetValueOrDefault(0) * 4;
                mysticCoinCount += (allSlots?.Where(i => i!.Id == 68330).Sum(i => i!.Count)).GetValueOrDefault(0) * 6;
                mysticCoinCount += (allSlots?.Where(i => i!.Id == 68333).Sum(i => i!.Count)).GetValueOrDefault(0) * 8;

                // Bags of Laurels
                laurelCount += (allSlots?.Where(i => i!.Id == 68314).Sum(i => i!.Count)).GetValueOrDefault(0);
                laurelCount += (allSlots?.Where(i => i!.Id == 68339).Sum(i => i!.Count)).GetValueOrDefault(0) * 2;
                laurelCount += (allSlots?.Where(i => i!.Id == 68327).Sum(i => i!.Count)).GetValueOrDefault(0) * 3;
                laurelCount += (allSlots?.Where(i => i!.Id == 68336).Sum(i => i!.Count)).GetValueOrDefault(0) * 4;
                laurelCount += (allSlots?.Where(i => i!.Id == 68328).Sum(i => i!.Count)).GetValueOrDefault(0) * 10;
                laurelCount += (allSlots?.Where(i => i!.Id == 68334).Sum(i => i!.Count)).GetValueOrDefault(0) * 15;
                laurelCount += (allSlots?.Where(i => i!.Id == 68351).Sum(i => i!.Count)).GetValueOrDefault(0) * 20;
            }

            account.SetCount("MysticCoin", mysticCoinCount);
            account.SetCount("Laurel", laurelCount.GetValueOrDefault(0));

        }
        catch (Exception e)
        {
        }

    }

    private async Task FetchApiDataGw2SDK(List<model.Account>? accounts)
    {
        if (accounts == null) return;

        using var http = new HttpClient(new SocketsHttpHandler(), true);
        http.UseGuildWars2();
        http.UseLanguage(Language.English);
        http.UseSchemaVersion(SchemaVersion.Recommended);

        var buildService = new BuildService(http, new BuildReader(), MissingMemberBehavior.Undefined);
        var characterService = new CharacterService(http, new CharacterReader(), MissingMemberBehavior.Undefined);
        var accountService = new AccountService(http, new AccountReader(), MissingMemberBehavior.Undefined);

        var build = await buildService.GetBuild();

        var fetchTasks = accounts
            .Where(a => !string.IsNullOrEmpty(a.ApiKey))
            .Select(a => FetchAccountDetailsGw2SDK(a, accountService, characterService))
            .ToList();

        await Task.WhenAll(fetchTasks);
    }

    public const int MysticCoinId = 19976;
    public const int LaurelId = 3;

    private async Task FetchAccountDetailsGw2SDK(model.Account account,
        AccountService accountService,
        CharacterService characterService)
    {
        var rand = new Random();

        try
        {
            var accountReturnTask = accountService.GetAccount(account.ApiKey);
            var charactersReturnTask = characterService.GetCharacters(account.ApiKey);

            var accountReturn = await accountReturnTask;
            if (accountReturn.Value != null)
            {
                account.CreatedAt = accountReturn.Value.Created.UtcDateTime;
                account.DisplayName = accountReturn.Value.Name;
            }

            var charactersReturn = await charactersReturnTask;
            if (charactersReturn.Values != null)
            {
                var prime = charactersReturn.Values.FirstOrDefault();
                if (prime != null)
                {
                    account.Character = prime.Name;

                    var allSlots = prime.Bags?.SelectMany(bag => bag?.Inventory ?? Array.Empty<InventorySlot>()).Where(i => i != null).ToList();

                    var mcCount = allSlots?.Where(i => i!.Id == MysticCoinId).Sum(i => i!.Count);
                    account.SetCount("MysticCoin", mcCount.GetValueOrDefault(0));
                }
            }
        }
        catch (Exception e)
        {
        }

        // TODO work-out how to access the wallet
        account.SetCount("Laurel", rand.Next(0, 100));
    }

    private void Initialise()
    {
        //Task.Run(() =>
        //{
#pragma warning disable CS4014
        accountCollection.Load();
#pragma warning restore CS4014
        // });

    }

}

