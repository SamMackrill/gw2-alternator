using System.Windows.Forms;

namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class SettingsViewModel : ObservableObject
{
    private readonly AccountCollection accountCollection;
    private Settings Settings { get; }
    private Func<string>? GetVersion { get; }

    public SettingsViewModel(Settings settings, AccountCollection accountCollection, Func<string>? getVersion)
    {
        this.accountCollection = accountCollection;
        Settings = settings;
        GetVersion = getVersion;
    }

    public string Gw2Folder
    {
        get => Settings.Gw2Folder;
        set => Settings.Gw2Folder = value;
    }

    public int MaxLoginInstances
    {
        get => Settings.MaxLoginInstances;
        set => Settings.MaxLoginInstances = value;
    }

    public string Title => $"GW2 alternator Settings V{GetVersion?.Invoke() ?? "?.?.?"}";

    public RelayCommand<object> ChooseGw2FolderCommand => new (_ =>
    {
        using var browser = new FolderBrowserDialog
        {
            Description = "Select Location of Guild Wars 2 exe",
            SelectedPath = Settings.Gw2Folder
        };
        var result = browser.ShowDialog();

        if (result != DialogResult.OK || string.IsNullOrWhiteSpace(browser.SelectedPath)) return;

        Settings.Gw2Folder = browser.SelectedPath;
    });

    public RelayCommand<object> ResetGw2FolderCommand => new(_ =>
    {
        Settings.Gw2Folder = SettingsController.DefaultSettings.Gw2Folder;
    });

    public RelayCommand<object> ResetMaxLoginInstancesCommand => new(_ =>
    {
        Settings.MaxLoginInstances = SettingsController.DefaultSettings.MaxLoginInstances;
    });


    public RelayCommand<object> ImportFromLaunchBuddyCommand => new(_ =>
    {
        accountCollection.ImportFromLaunchbuddy();
    }, _ => accountCollection.CanImportFromLaunchbuddy);
}