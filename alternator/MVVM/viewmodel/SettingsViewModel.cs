namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class SettingsViewModel : ObservableObject
{
    private Settings Settings { get; }
    private Func<string>? GetVersion { get; }

    public SettingsViewModel(Settings settings, Func<string>? getVersion)
    {
        Settings = settings;
        GetVersion = getVersion;
    }

    public string Gw2Folder
    {
        get => Settings.Gw2Folder;
        set => Settings.Gw2Folder = value;
    }

    public string Title => $"GW2 alternator Settings V{GetVersion?.Invoke() ?? "?.?.?"}";

}