namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class VpnConnectionsViewModel : ObservableObject
{

    public ObservableCollectionEx<VpnConnectionViewModel> VpnConnections { get; }

    public VpnConnectionsViewModel()
    {
        VpnConnections = new ObservableCollectionEx<VpnConnectionViewModel>();
    }

    public void Add(IEnumerable<VpnDetails>? vpnConnections)
    {
        if (vpnConnections == null) return;
        VpnConnections.AddRange(vpnConnections.Select(v => new VpnConnectionViewModel(v)));
    }

}