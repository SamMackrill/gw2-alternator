using System.Text.Json;

namespace guildwars2.tools.alternator.MVVM.model;

public class VpnCollection : JsonCollection<VpnDetails>
{
    private const string vpnFileName = "vpnconnections.json";

    public VpnCollection(FileSystemInfo folderPath) : base(folderPath, vpnFileName) { }

    public List<VpnDetails>? Vpns => Items;

    public override event EventHandler? Loaded;
    public override event EventHandler? LoadFailed;

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

            await Task.Delay(1000);
            Logger.Debug("VPNs saved to {0}", vpnJson);
        }
        finally
        {
            semaphore.Release();
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

    public VpnDetails? GetVpn(string key)
    {
        return Vpns?.FirstOrDefault(v => v.Id == key) ?? new VpnDetails();
    }

    public List<AccountVpnViewModel> GetAccountVpns(IAccount account)
    {
        return Vpns?.Select(v => new AccountVpnViewModel(v, account)).ToList() ?? new List<AccountVpnViewModel>();
    }
}