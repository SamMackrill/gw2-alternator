﻿namespace guildwars2.tools.alternator.MVVM.viewmodel;

public class VpnConnectionsViewModel : ObservableObject
{

    public IRelayCommand? AddNewConnectionCommand { get; }
    public IRelayCommand? DeleteCommand { get; }

    public ObservableCollectionEx<VpnConnectionViewModel> VpnConnections { get; }

    private readonly IVpnCollection vpnCollection;
    private readonly ISettingsController settingsController;

    public VpnConnectionsViewModel(IVpnCollection vpnCollection, ISettingsController settingsController)
    {
        this.vpnCollection = vpnCollection;

        VpnConnections = new ObservableCollectionEx<VpnConnectionViewModel>();
        VpnConnections.CollectionChanged += VpnConnections_CollectionChanged;
        connectionNames = new List<string>();

        this.settingsController = settingsController;
        settingsController.PropertyChanged += SettingsController_PropertyChanged;

        AddNewConnectionCommand = new RelayCommand<object>(_ =>
        {
            var newVpnDetails = vpnCollection.AddNew();
            VpnConnections.Add(new VpnConnectionViewModel(newVpnDetails, this));
        });

        DeleteCommand = new RelayCommand<object>(_ =>
        {
           foreach (var deadVpn in VpnConnections.Where(i => i.IsSelected).ToList())
           {
               VpnConnections.Remove(deadVpn);
               vpnCollection.Remove(deadVpn.VpnDetails);
           }
           OnPropertyChanged(nameof(VpnConnections));
        });

    }

    private readonly Dictionary<string, List<string>> propertyConverter = new()
    {
        { "FontSize", new() { "HeaderFontSize" } },
    };

    private void SettingsController_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        args.PassOnChanges(OnPropertyChanged, propertyConverter);
    }

    private void VpnConnections_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Remove || e.OldItems == null) return;
        foreach (VpnConnectionViewModel deadVpn in e.OldItems)
        {
            vpnCollection.Remove(deadVpn.VpnDetails);
        }
    }

    public double FontSize => settingsController.Settings?.FontSize ?? SettingsController.DefaultSettings.FontSize;
    public double HeaderFontSize => settingsController.Settings?.HeaderFontSize ?? SettingsController.DefaultSettings.FontSize;


    public void Update()
    {
        if (vpnCollection.Vpns == null) return;
        VpnConnections.Clear();
        VpnConnections.AddRange(vpnCollection.Vpns.Select(v => new VpnConnectionViewModel(v, this)).OrderBy(v => v.ConnectionName));
    }


    private Task? lookupTask;
    private CancellationTokenSource? cancellationTokenSource;

    private readonly List<string> connectionNames;

    public IEnumerable<string> ConnectionNames => connectionNames.OrderBy(c => c);

    public void LookupConnections()
    {
        if (lookupTask is {IsCompleted: false})
        {
            cancellationTokenSource?.Cancel("Connection lookup failed");
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
                connectionNames.Clear();
                connectionNames.AddRange(ExtractConnections(lines, settingsController.Settings?.VpnMatch ?? @"\w+-\w+-st\d+\.prod\.surfshark\.com"));
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
    public static List<string> ExtractConnections(IEnumerable<string>? lines, string vpnMatch)
    {
        var phoneNumberPattern = new Regex(@$"^PhoneNumber=\s*{vpnMatch}", RegexOptions.IgnoreCase);
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
            if (phoneNumberPattern.IsMatch(line) && connectionName != null)
            {
                connections.Add(connectionName);
                connectionName = null;
            }
        }
        return connections;
    }

}