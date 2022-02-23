using System.Text.Json;

namespace guildwars2.tools.alternator.MVVM.model;

public interface IVpnCollection : IJsonCollection
{
    List<VpnDetails>? Vpns { get; }
    VpnDetails GetVpn(string key);
    List<AccountVpnViewModel> GetAccountVpns(IAccount account);
    VpnDetails AddNew();
    void Remove(VpnDetails deadVpn);
    bool Any();
}

public class VpnCollection : JsonCollection<VpnDetails>, IVpnCollection
{
    private const string vpnFileName = "vpnconnections.json";

    public VpnCollection(FileSystemInfo folderPath) : base(folderPath, vpnFileName) { }

    public List<VpnDetails>? Vpns => Items;

    public override event EventHandler? Loaded;
    public override event EventHandler? LoadFailed;
    public override event EventHandler? Updated;

    public override async Task Save()
    {
        try
        {
            Logger.Debug("Saving VPNs to {0}", vpnJson);
            await semaphore.WaitAsync();

            await using (var stream = new FileStream(vpnJson, FileMode.Create))
            {
                await JsonSerializer.SerializeAsync(stream, Items, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            }

            Logger.Debug("VPNs saved to {0}", vpnJson);
        }
        finally
        {
            semaphore.Release();
            OnPropertyChanged(nameof(Vpns));
        }
    }

    public override async Task Load()
    {
        try
        {
            await semaphore.WaitAsync();
            if (!File.Exists(vpnJson)) throw new FileNotFoundException();
            await using var stream = File.OpenRead(vpnJson);
            Items = await JsonSerializer.DeserializeAsync<List<VpnDetails>>(stream);
            Logger.Debug("VPNs loaded from {0}", vpnJson);
            Loaded?.Invoke(this, EventArgs.Empty);
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading VPNs from {vpnJson}");
            LoadFailed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public VpnDetails GetVpn(string key)
    {
        return Vpns?.FirstOrDefault(v => v.Id == key) ?? new VpnDetails();
    }

    public List<AccountVpnViewModel> GetAccountVpns(IAccount account)
    {
        return Vpns?.Select(v => new AccountVpnViewModel(v, account)).ToList() ?? new List<AccountVpnViewModel>();
    }

    public VpnDetails AddNew()
    {
        var newVpnDetails = new VpnDetails { Id = "New" };
        Items ??= new List<VpnDetails>();
        Items.Add(newVpnDetails);
        OnPropertyChanged(nameof(Vpns));
        Updated?.Invoke(this, EventArgs.Empty);

        return newVpnDetails;
    }

    public void Remove(VpnDetails deadVpn)
    {
        Vpns?.Remove(deadVpn);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public bool Any() => Vpns?.Any() ?? false;
}