using System.Text.Json;

namespace guildwars2.tools.alternator.MVVM.model;

[Serializable]
public class Settings : ObservableObject
{

    private string? gw2Folder;
    public string? Gw2Folder
    {
        get => gw2Folder;
        set => SetProperty(ref gw2Folder, value);
    }

}