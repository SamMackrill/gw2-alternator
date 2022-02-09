
namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class VpnConnectionsViewModel : ObservableObject
{

    public ICommandExtended? AddNewConnectionCommand { get; }

    public ObservableCollectionEx<VpnConnectionViewModel> VpnConnections { get; }

    private readonly VpnCollection vpnCollection;

    public VpnConnectionsViewModel(VpnCollection vpnCollection)
    {
        this.vpnCollection = vpnCollection;

        VpnConnections = new ObservableCollectionEx<VpnConnectionViewModel>();
        ConnectionNames = new List<string>();

        AddNewConnectionCommand = new RelayCommand<object>(_ =>
        {
            var newVpnDetails = vpnCollection.AddNew();
            VpnConnections.Add(new VpnConnectionViewModel(newVpnDetails, this));
        });

    }

    public void Update()
    {
        if (vpnCollection.Vpns == null) return;
        VpnConnections.Clear();
        VpnConnections.AddRange(vpnCollection.Vpns.Select(v => new VpnConnectionViewModel(v, this)).OrderBy(v => v.ConnectionName));
    }


    private Task? lookupTask;
    private CancellationTokenSource? cancellationTokenSource;

    public List<string> ConnectionNames { get; private init; }

    public void LookupConnections()
    {
        if (lookupTask is {IsCompleted: false})
        {
            cancellationTokenSource?.Cancel();
            lookupTask.Wait();
        }
        cancellationTokenSource = new CancellationTokenSource();

        lookupTask = Task.Run(async () =>
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var phoneBook = new FileInfo(Path.Combine(appData, @"Microsoft\Network\Connections\Pbk\rasphone.pbk"));
                if (!phoneBook.Exists) return;
                var lines = await File.ReadAllLinesAsync(phoneBook.FullName);
                ConnectionNames.Clear();
                ConnectionNames.AddRange(ExtractConnections(lines));
            }
            finally
            {
                OnPropertyChanged(nameof(ConnectionNames));
                OnPropertyChanged(nameof(HasConnections));
            }
        }, cancellationTokenSource.Token);
    }

    public bool HasConnections => ConnectionNames.Any();

    private static readonly Regex NameMatchPattern = new(@"^\[([^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneNumberPattern = new(@"^PhoneNumber=\s*\w+-\w+-st\d+.prod.surfshark.com", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static List<string> ExtractConnections(IEnumerable<string>? lines)
    {
        string? connectionName = null;
        var connections = new List<string>();
        foreach (var line in lines ?? new List<string>())
        {
            var nameMatch = NameMatchPattern.Match(line);
            if (nameMatch.Success)
            {
                connectionName = nameMatch.Groups[1].Value;
                continue;
            }
            if (PhoneNumberPattern.IsMatch(line) && connectionName != null)
            {
                connections.Add(connectionName);
                connectionName = null;
            }
        }
        return connections;
    }

}