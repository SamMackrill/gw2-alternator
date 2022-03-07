namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class SettingsViewModel : ObservableObject
{
    private readonly ISettingsController settingsController;
    private readonly IAccountCollection accountCollection;
    private readonly IDialogService dialogService;
    private Func<string>? GetVersion { get; }

    private Settings Settings => settingsController.Settings!;

    public SettingsViewModel(
        ISettingsController settingsController, 
        IAccountCollection accountCollection,
        Func<string>? getVersion, 
        IDialogService dialogService)
    {
        this.settingsController = settingsController;
        this.accountCollection = accountCollection;
        this.dialogService = dialogService;
        GetVersion = getVersion;
        if (settingsController.Settings != null) settingsController.Settings.PropertyChanged += ModelPropertyChanged;
    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "AlwaysIgnoreVpn", new() { nameof(VpnVisibility) } },
    };

    private void ModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }

    public string? Gw2Folder
    {
        get => Settings.Gw2Folder;
        set => Settings.Gw2Folder = value;
    }

    public int MaxLoginInstances
    {
        get => Settings.MaxLoginInstances;
        set => Settings.MaxLoginInstances = value;
    }

    public int AccountBand1
    {
        get => Settings.AccountBand1;
        set => Settings.AccountBand1 = value;
    }
    public int AccountBand1Delay
    {
        get => Settings.AccountBand1Delay;
        set => Settings.AccountBand1Delay = value;
    }

    public int AccountBand2
    {
        get => Settings.AccountBand2;
        set => Settings.AccountBand2 = value;
    }
    public int AccountBand2Delay
    {
        get => Settings.AccountBand2Delay;
        set => Settings.AccountBand2Delay = value;
    }

    public int AccountBand3
    {
        get => Settings.AccountBand3;
        set => Settings.AccountBand3 = value;
    }

    public int AccountBand3Delay
    {
        get => Settings.AccountBand3Delay;
        set => Settings.AccountBand3Delay = value;
    }

    public int StuckTimeout
    {
        get => Settings.StuckTimeout;
        set => Settings.StuckTimeout = value;
    }

    public int VpnAccountCount
    {
        get => Settings.VpnAccountCount;
        set => Settings.VpnAccountCount = value;
    }

    public ErrorDetection ExperimentalErrorDetection
    {
        get => Settings.ExperimentalErrorDetection;
        set => Settings.ExperimentalErrorDetection = value;
    }

    public bool AlwaysIgnoreVpn
    {
        get => Settings.AlwaysIgnoreVpn;
        set => Settings.AlwaysIgnoreVpn = value;
    }

    public string? VpnMatch
    {
        get => Settings.VpnMatch;
        set => Settings.VpnMatch = value;
    }

    public int FontSize
    {
        get => Settings.FontSize;
        set => Settings.FontSize = value;
    }

    public double HeaderFontSize => Settings.HeaderFontSize;

    public Visibility VpnVisibility => settingsController.Settings?.AlwaysIgnoreVpn ?? true ? Visibility.Hidden : Visibility.Visible;

    public Array ErrorDetectionArray => Enum.GetValues(typeof(ErrorDetection));

    public string Title => $"GW2 Alternator Settings V{GetVersion?.Invoke() ?? "?.?.?"}";

    public RelayCommand<object> ChooseGw2FolderCommand => new (_ =>
    {
        var browser = new FolderBrowserDialogSettings
        {
            Description = "Select Location of Guild Wars 2 exe",
            SelectedPath = Settings.Gw2Folder ?? ""
        };
        var dialogService = Ioc.Default.GetService<IDialogService>();

        var success = dialogService?.ShowFolderBrowserDialog(this, browser) ?? false;
        if (!success || string.IsNullOrWhiteSpace(browser.SelectedPath)) return;

        Settings.Gw2Folder = browser.SelectedPath;
    });

    public RelayCommand<object> ResetGw2FolderCommand => new(_ =>
    {
        settingsController.DiscoverGw2ExeLocation();
    });

    public RelayCommand<object> ResetMaxLoginInstancesCommand => new(_ =>
    {
        Settings.MaxLoginInstances = SettingsController.DefaultSettings.MaxLoginInstances;
    });

    public RelayCommand<object> ResetBand1Command => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.AccountBand1 = defaultSettings.AccountBand1;
        Settings.AccountBand1Delay = defaultSettings.AccountBand1Delay;
    });

    public RelayCommand<object> ResetBand2Command => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.AccountBand2 = defaultSettings.AccountBand2;
        Settings.AccountBand2Delay = defaultSettings.AccountBand2Delay;
    });

    public RelayCommand<object> ResetBand3Command => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.AccountBand3 = defaultSettings.AccountBand3;
        Settings.AccountBand3Delay = defaultSettings.AccountBand3Delay;
    });
    public RelayCommand<object> ResetStuckTimeoutCommand => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.StuckTimeout = defaultSettings.StuckTimeout;
    });
    public RelayCommand<object> ResetVpnAccountCountCommand => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.VpnAccountCount = defaultSettings.VpnAccountCount;
    });
    public RelayCommand<object> ResetErrorDetectionCommand => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.ExperimentalErrorDetection = defaultSettings.ExperimentalErrorDetection;
    });
    public RelayCommand<object> ResetAlwaysIgnoreVpnCommand => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.AlwaysIgnoreVpn = defaultSettings.AlwaysIgnoreVpn;
    });
    public RelayCommand<object> ResetVpnMatchCommand => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.VpnMatch = defaultSettings.VpnMatch;
    });
    public RelayCommand<object> ResetFontSizeCommand => new(_ =>
    {
        var defaultSettings = SettingsController.DefaultSettings;
        Settings.FontSize = defaultSettings.FontSize;
    });
    public RelayCommand<object> ResetAllCommand => new(_ =>
    {
        settingsController.ResetAll();
    });

    public RelayCommand<object> ImportFromLaunchBuddyCommand => new(_ =>
    {
        int count = accountCollection.ImportFromLaunchbuddy();

        _ = dialogService.ShowMessageBox(
            this,
            $"{count} accounts imported from GW2 Launchbuddy",
            "GW2-Alternator",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    }, _ => accountCollection.CanImportFromLaunchbuddy);

    public RelayCommand<object> ImportFromLauncherCommand => new(_ =>
    {
        int count = accountCollection.ImportFromLauncher();
        _ = dialogService.ShowMessageBox(
            this,
            $"{count} accounts imported from GW2 Launcher",
            "GW2-Alternator",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    }, _ => accountCollection.CanImportFromLauncher);
}