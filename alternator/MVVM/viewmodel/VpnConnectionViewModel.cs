namespace guildwars2.tools.alternator.MVVM.viewmodel;


[DebuggerDisplay("{" + nameof(DebugDisplay) + ",nq}")]
public class VpnConnectionViewModel : ObservableObject
{
    private readonly VpnDetails vpnDetails;
    private readonly VpnConnectionsViewModel parent;
    public ICommandExtended? UndoConnectionNameCommand { get; }
    public AsyncCommand? TestConnectionCommand { get; }

    public string? Id
    {
        get => vpnDetails.Id;
        set => vpnDetails.Id = value;
    }

    public string? ConnectionName
    {
        get => vpnDetails.ConnectionName;
        set => vpnDetails.ConnectionName = value;
    }

    public VpnDetails VpnDetails => vpnDetails;

    private string? status;

    public string? Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

    public VpnConnectionViewModel(VpnDetails vpnDetails, VpnConnectionsViewModel parent)
    {
        this.vpnDetails = vpnDetails;
        vpnDetails.PropertyChanged += External_PropertyChanged;
        this.parent = parent;
        parent.PropertyChanged += External_PropertyChanged;

        UndoConnectionNameCommand = new RelayCommand<object>(_ =>
        {
            vpnDetails.Undo();
        });

        TestConnectionCommand = new AsyncCommand( async () =>
        {
            var cts = new CancellationTokenSource();
            Status = await vpnDetails.Test(cts.Token);
        });

    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        //{ "ConnectionName", new() { nameof(ConnectionNames) } },
    };

    private void External_PropertyChanged(object? sender, PropertyChangedEventArgs args)
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