using System.Windows.Forms;

namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel main;

    public SettingsViewModel(MainViewModel main)
    {
        this.main = main;
    }

    public string Gw2Folder { get; set; }

    public string Title => $"GW2 alternator Settings V{main.Version?.ToString() ?? "0.0.0 (dev)"}";


}