namespace guildwars2.tools.alternator.MVVM.viewmodel;


[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VpnConnectionViewModel : ObservableObject
{
    private readonly VpnDetails vpnDetails;
    public ICommandExtended? PasteConnectionNameCommand { get; }
    public ICommandExtended? UndoConnectionNameCommand { get; }
    public ICommandExtended? AddNewConnectionCommand { get; }
    public ICommandExtended? TestConnectionCommand { get; }

    public string Id
    {
        get => vpnDetails.Id;
        set => vpnDetails.Id = value;
    }

    public string ConnectionName
    {
        get => vpnDetails.ConnectionName;
        set => vpnDetails.ConnectionName = value;
    }

    private string? status;

    public string? Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

    public VpnConnectionViewModel(VpnDetails vpnDetails)
    {
        this.vpnDetails = vpnDetails;
        vpnDetails.PropertyChanged += Account_PropertyChanged;

        PasteConnectionNameCommand = new RelayCommand<object>(_ =>
        {
            var pasteText = Clipboard.GetText();
            ConnectionName = pasteText;
        });

        UndoConnectionNameCommand = new RelayCommand<object>(_ =>
        {
            vpnDetails.Undo();
        });

        AddNewConnectionCommand = new RelayCommand<object>(_ =>
        {
        });

        TestConnectionCommand = new RelayCommand<object>(_ =>
        {
        });

    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
    };


    private async void Account_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null) return;
        var propertyNames = new List<string> { args.PropertyName };
        if (propertyConverter.ContainsKey(args.PropertyName)) propertyNames.AddRange(propertyConverter[args.PropertyName]);
        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    public bool IsSelected { get; set; }

    private string DebugDisplay => vpnDetails.ToString();

}