using System.Text.Json;
using System.Xml.Linq;

namespace guildwars2.tools.alternator;

public class SettingsController : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string SettingsJsonFile = "settings.json";

    private readonly string settingsJson;
    private readonly SemaphoreSlim semaphore;

    public FileInfo? DatFile { get; set; }
    public FileInfo? GfxSettingsFile { get; set; }
    public Settings? Settings { get; private set; }


    public SettingsController(FileSystemInfo folderPath)
    {
        settingsJson = Path.Combine(folderPath.FullName, SettingsJsonFile);
        semaphore = new SemaphoreSlim(1, 1);
    }

    public void Load()
    {
        try
        {
            semaphore.Wait();
            using var stream = File.OpenRead(settingsJson);
            var settings = JsonSerializer.Deserialize<Settings>(stream);
            Logger.Debug("Settings loaded from {0}", settingsJson);
            Settings = settings ?? DefaultSettings;
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading Settings from {settingsJson}");
        }
        finally
        {
            semaphore.Release();
        }
        Settings = DefaultSettings;
    }

    public async Task LoadAsync()
    {
        try
        {
            await semaphore.WaitAsync();
            await using var stream = File.OpenRead(settingsJson);
            var settings = await JsonSerializer.DeserializeAsync<Settings>(stream);
            Logger.Debug("Settings loaded from {0}", settingsJson);
            Settings = settings ?? DefaultSettings;
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading Settings from {settingsJson}");
        }
        finally
        {
            semaphore.Release();
        }
        Settings = DefaultSettings;
    }

    public void Save()
    {
        if (Settings == null) return;
        try
        {
            Logger.Debug("Saving Settings to {0}", settingsJson);
            semaphore.Wait();

            using (var stream = new FileStream(settingsJson, FileMode.Create))
            {
                 JsonSerializer.Serialize(stream, Settings, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            }

            Task.Delay(1000);
            Logger.Debug("Settings saved to {0}", settingsJson);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task SaveAsync()
    {
        if (Settings == null) return;
        try
        {
            Logger.Debug("Saving Settings to {0}", settingsJson);
            await semaphore.WaitAsync();

            await using (var stream = new FileStream(settingsJson, FileMode.Create))
            {
                await JsonSerializer.SerializeAsync(stream, Settings, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            }

            await Task.Delay(1000);
            Logger.Debug("Settings saved to {0}", settingsJson);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static Settings DefaultSettings =>
        new()
        {
            Gw2Folder = null,
            MaxLoginInstances = 4,
            AccountBand1 = 10,
            AccountBand1Delay = 5,
            AccountBand2 = 24,
            AccountBand2Delay = 20,
            AccountBand3 = 40,
            AccountBand3Delay = 45,
            StuckTimeout = 30,
            VpnAccountCount = 10,
        };


    public void DiscoverGw2ExeLocation( )
    {
        if (Settings==null || GfxSettingsFile is not {Exists: true} || Directory.Exists(Settings.Gw2Folder)) return;

        var doc = XDocument.Load(GfxSettingsFile.FullName);
        var installPath = doc.Descendants("INSTALLPATH").FirstOrDefault();

        var valueAttribute = installPath?.Attribute("Value");
        if (valueAttribute != null && Directory.Exists(valueAttribute.Value)) Settings.Gw2Folder = valueAttribute.Value;
        if (Directory.Exists(@"C:\Program Files (x86)\Guild Wars 2")) Settings.Gw2Folder = @"C:\Program Files (x86)\Guild Wars 2";

    }
}