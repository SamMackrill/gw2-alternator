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

public interface ISettingsController
{
    FileInfo? DatFile { get; set; }
    FileInfo? GfxSettingsFile { get; set; }
    Settings? Settings { get; }
    string MetricsFile { get; }
    string UniqueMetricsFile { get; }
    DirectoryInfo ApplicationFolder { get; }
    void Load();
    void Save();
    Task SaveAsync();
    void DiscoverGw2ExeLocation( );
    void ResetAll();
    event PropertyChangedEventHandler? PropertyChanged;
    event PropertyChangingEventHandler? PropertyChanging;
}

public class SettingsController : ObservableObject, ISettingsController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string SettingsJsonFile = "settings.json";

    public DirectoryInfo ApplicationFolder { get; }

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

    public SettingsController(DirectoryInfo folderPath)
    {
        ApplicationFolder = folderPath;
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
            ValidateSettings();
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
        DiscoverGw2ExeLocation();
    }

    private void ValidateSettings()
    {
        DiscoverGw2ExeLocation();
        var defaults = DefaultSettings;
        if (Settings!.MaxLoginInstances == default) Settings!.MaxLoginInstances = defaults.MaxLoginInstances;
        if (Settings!.StuckTimeout == default) Settings!.StuckTimeout = defaults.StuckTimeout;
        if (Settings!.LaunchTimeout == default) Settings!.LaunchTimeout = defaults.LaunchTimeout;
        if (Settings!.CrashWaitDelay == default) Settings!.CrashWaitDelay = defaults.CrashWaitDelay;
        if (Settings!.VpnAccountCount == default) Settings!.VpnAccountCount = defaults.VpnAccountCount;
        if (Settings!.AuthenticationMemoryThreshold == default) Settings!.AuthenticationMemoryThreshold = defaults.AuthenticationMemoryThreshold;
        if (Settings!.CharacterSelectedMemoryThreshold == default) Settings!.CharacterSelectedMemoryThreshold = defaults.CharacterSelectedMemoryThreshold;
        if (Settings!.DeltaMemoryThreshold == default) Settings!.DeltaMemoryThreshold = defaults.DeltaMemoryThreshold;
        if (Settings!.ShutDownDelay == default) Settings!.ShutDownDelay = defaults.ShutDownDelay;
        if (Settings!.FontSize == default) Settings!.FontSize = defaults.FontSize;
        Settings!.VpnMatch ??= defaults.VpnMatch;
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
        settings.LaunchTimeout = 90;
        settings.CrashWaitDelay = 6;
        settings.VpnAccountCount = 10;
        settings.AuthenticationMemoryThreshold = 120;
        settings.CharacterSelectedMemoryThreshold = 1400;
        settings.DeltaMemoryThreshold = 100;
        settings.ShutDownDelay = 500;
        settings.FontSize = 12;
        settings.ExperimentalErrorDetection = ErrorDetection.Delay;
        settings.AlwaysIgnoreVpn = true;
        settings.VpnMatch = @"\w+-\w+-st\d+\.prod\.surfshark\.com";
    }

    public string MetricsFile => Path.Combine(ApplicationFolder.FullName, "gw2-alternator-metrics.txt");
    public string UniqueMetricsFile => Path.Combine(ApplicationFolder.FullName, $"gw2-alternator-metrics_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.txt");


    public void DiscoverGw2ExeLocation()
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