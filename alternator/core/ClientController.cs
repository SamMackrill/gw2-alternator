
namespace guildwars2.tools.alternator;

public class ClientController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly FileInfo loginFile;
    private readonly FileInfo gfxSettingsFile;
    private readonly LaunchType launchType;
    private readonly SemaphoreSlim loginSemaphore;

    public event EventHandler<GenericEventArgs<bool>>? AfterLaunch;

    public ClientController(FileInfo loginFile, FileInfo gfxSettingsFile, LaunchType launchType)
    {
        this.loginFile = loginFile;
        this.gfxSettingsFile = gfxSettingsFile;
        this.launchType = launchType;
        loginSemaphore = new SemaphoreSlim(0, 1);
    }

    public async Task Launch(List<Account> accounts, int maxInstances, CancellationToken launchCancelled)
    {
        if (!accounts.Any())
        {
            Logger.Debug("No accounts to run.");
            return;
        }
        Logger.Debug("Max GW2 Instances={0}", maxInstances);
        var exeSemaphore = new SemaphoreSlim(0, maxInstances);
        var launchCount = new Counter();
        bool LastLaunch() => launchCount.Count == accounts.Count;
        var tasks = accounts.Select(account => Task.Run(async () =>
            {
                var launcher = new Launcher(account, launchType, launchCancelled);
                var success = await launcher.Launch(loginFile, gfxSettingsFile, loginSemaphore, exeSemaphore, 3, LastLaunch, launchCount);
                AfterLaunch?.Invoke(account, new GenericEventArgs<bool>(success));
                LogManager.Flush();
            }, launchCancelled))
            .ToList();
        Logger.Debug("{0} threads primed.", tasks.Count);
        // Allow all the tasks to start and block.
        await Task.Delay(200, launchCancelled);
        if (launchCancelled.IsCancellationRequested) return;

        // Release the hounds
        exeSemaphore.Release(maxInstances);
        loginSemaphore.Release(1);

        await Task.WhenAll(tasks.ToArray());

        Logger.Debug("All thread exited.");
    }

    public async Task Restore()
    {
        await loginSemaphore.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                SafeRestoreBackup(loginFile);
                SafeRestoreBackup(gfxSettingsFile);
            });
        }
        finally
        {
            loginSemaphore.Release();
        }
    }

    private void SafeRestoreBackup(FileInfo file)
    {
        var backup = new FileInfo($"{file.FullName}.bak");
        if (!backup.Exists) return;
        try
        {
            file.Delete(); // Symbolic links need to be specifically delete
            backup.MoveTo(file.FullName);
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Could not restore {file} from backup!");
        }
    }
}