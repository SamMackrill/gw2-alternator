using System.Text.Json;
using System.Xml.Linq;

namespace guildwars2.tools.alternator;

public enum ErrorDetection
{
    [Description("Delay")]
    Delay,
    [Description("Delay & Pixel")]
    DelayAndPixel,
}

public class SettingsController : ObservableObject
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string SettingsJsonFile = "settings.json";

    private FileSystemInfo SourceFolder { get; }
    private readonly string settingsJson;
    private readonly SemaphoreSlim semaphore;

    public FileInfo? DatFile { get; set; }
    public FileInfo? GfxSettingsFile { get; set; }

    private Settings? settings;
    public Settings? Settings
    {
        get => settings;
        private set
        {
            settings = value;
            if (settings != null) settings.PropertyChanged += Settings_PropertyChanged;
        }
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
    }

    public SettingsController(FileSystemInfo folderPath)
    {
        SourceFolder = folderPath;
        settingsJson = Path.Combine(folderPath.FullName, SettingsJsonFile);
        semaphore = new SemaphoreSlim(1, 1);
    }

    public void Load()
    {
        try
        {
            semaphore.Wait();
            using var stream = File.OpenRead(settingsJson);
            var settingsFromFile = JsonSerializer.Deserialize<Settings>(stream);
            Logger.Debug("Settings loaded from {0}", settingsJson);
            Settings = settingsFromFile ?? DefaultSettings;
            return;
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

    public static Settings DefaultSettings
    {
        get
        {
            var settings = new Settings();
            Reset(settings);
            return settings;
        }
    }

    private static void Reset(Settings settings)
    {
        settings.Gw2Folder = null;
        settings.MaxLoginInstances = 4;
        settings.AccountBand1 = 10;
        settings.AccountBand1Delay = 5;
        settings.AccountBand2 = 24;
        settings.AccountBand2Delay = 20;
        settings.AccountBand3 = 40;
        settings.AccountBand3Delay = 45;
        settings.StuckTimeout = 30;
        settings.VpnAccountCount = 10;
        settings.ExperimentalErrorDetection = ErrorDetection.Delay;
        settings.AlwaysIgnoreVpn = false;
    }

    public string MetricsFile => Path.Combine(SourceFolder.FullName, "gw2-alternator-metrics.txt");
    public string UniqueMetricsFile => Path.Combine(SourceFolder.FullName, $"gw2-alternator-metrics_{DateTime.UtcNow:yyyy-dd-MM_HH-mm-ss}");


    public void DiscoverGw2ExeLocation( )
    {
        if (Settings==null || GfxSettingsFile is not {Exists: true} || Directory.Exists(Settings.Gw2Folder)) return;

        var doc = XDocument.Load(GfxSettingsFile.FullName);
        var installPath = doc.Descendants("INSTALLPATH").FirstOrDefault();

        var valueAttribute = installPath?.Attribute("Value");
        if (valueAttribute != null && Directory.Exists(valueAttribute.Value)) Settings.Gw2Folder = valueAttribute.Value;
        if (Directory.Exists(@"C:\Program Files (x86)\Guild Wars 2")) Settings.Gw2Folder = @"C:\Program Files (x86)\Guild Wars 2";

    }

    public void ResetAll()
    {
        Settings ??= new Settings();
        Reset(Settings);
        DiscoverGw2ExeLocation();
    }
}