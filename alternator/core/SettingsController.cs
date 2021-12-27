using System.Text.Json;

namespace guildwars2.tools.alternator;

public class SettingsController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const string SettingsJsonFile = "settings.json";

    private readonly string settingsJson;
    private readonly SemaphoreSlim semaphore;


    public SettingsController(FileSystemInfo folderPath)
    {
        settingsJson = Path.Combine(folderPath.FullName, SettingsJsonFile);
        semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<Settings> Load()
    {
        try
        {
            await semaphore.WaitAsync();
            await using var stream = File.OpenRead(settingsJson);
            var settings = await JsonSerializer.DeserializeAsync<Settings>(stream);
            Logger.Debug("Settings loaded from {0}", settingsJson);
            return settings ?? DefaultSettings;
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Loading accounts from {settingsJson}");
        }
        finally
        {
            semaphore.Release();
        }
        return DefaultSettings;
    }

    public async Task Save(Settings? settings)
    {
        if (settings == null) return;
        try
        {
            Logger.Debug("Saving Accounts to {0}", settingsJson);
            await semaphore.WaitAsync();

            await using (var stream = new FileStream(settingsJson, FileMode.Create))
            {
                await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { AllowTrailingCommas = true, WriteIndented = true });
            }

            await Task.Delay(1000);
            Logger.Debug("Accounts saved to {0}", settingsJson);
        }
        finally
        {
            semaphore.Release();
        }
    }
    

    public static Settings DefaultSettings => new()
    {
        Gw2Folder = @"G:\Games\gw2"
    };
}