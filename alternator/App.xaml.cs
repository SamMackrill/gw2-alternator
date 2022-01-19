global using System;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.ComponentModel;
global using System.Xaml;

global using System.Threading;
global using System.Threading.Tasks;

global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;

global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Collections.Specialized;
global using System.Collections.Concurrent;

global using System.Windows;
global using System.Windows.Data;
global using System.Windows.Controls;
global using System.Windows.Threading;
global using System.Windows.Input;


global using System.Drawing;
global using System.Drawing.Imaging;

global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Runtime.InteropServices.ComTypes;


global using guildwars2.tools.alternator.MVVM.model;
global using guildwars2.tools.alternator.MVVM.view;
global using guildwars2.tools.alternator.MVVM.viewmodel;

global using AsyncAwaitBestPractices.MVVM;

global using NLog;


namespace guildwars2.tools.alternator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public string ApplicationName { get; }

    private SettingsController? settingsController;
    private AccountCollection? accountCollection;
    private VpnCollection? vpnCollection;

    public App()
    {
        ApplicationName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "gw2-alternator";
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);


        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var applicationFolder = new DirectoryInfo(Path.Combine(appData, ApplicationName));
        if (!applicationFolder.Exists) applicationFolder.Create();
        SetLogging(applicationFolder);

        settingsController = new SettingsController(applicationFolder);
        settingsController.Load();

        accountCollection = new AccountCollection(applicationFolder, Path.Combine(appData, @"Gw2 Launchbuddy"), Path.Combine(appData, @"Gw2Launcher"));

        vpnCollection = new VpnCollection(applicationFolder);


        var mainView = new MainViewModel(applicationFolder, appData, settingsController, accountCollection, vpnCollection);
        var mainWindow = new MainWindow(mainView);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        settingsController?.Save();
        LogManager.Shutdown();
        base.OnExit(e);
    }

    private void SetLogging(DirectoryInfo folder)
    {
        var config = new NLog.Config.LoggingConfiguration();
        // Targets where to log to: File and Console
        var logfile = new NLog.Targets.FileTarget("logfile")
        {
            FileName = Path.Combine(folder.FullName, "gw2-alternator-log.txt"), 
            DeleteOldFileOnStartup = true
        };
        var debugLogfile = new NLog.Targets.FileTarget("debuglogfile")
        {
            FileName = Path.Combine(folder.FullName, "gw2-alternator-debug-log.txt"),
            DeleteOldFileOnStartup = true
        };
        var errorLogfile = new NLog.Targets.FileTarget("errorlogfile")
        {
            FileName = Path.Combine(folder.FullName, "gw2-alternator-error-log.txt"),
            DeleteOldFileOnStartup = false
        };
        var logConsole = new NLog.Targets.ConsoleTarget("logconsole");

        // Rules for mapping loggers to targets            
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, logConsole);
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, debugLogfile);
        config.AddRule(LogLevel.Error, LogLevel.Fatal, errorLogfile);
        config.AddRule(LogLevel.Info, LogLevel.Info, logfile);

        // Apply config           
        LogManager.Configuration = config;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {


        // Process unhandled exception
        var shutdown = e.Exception is not XamlParseException;
        shutdown = !e.Exception.GetType().IsAssignableFrom(typeof(XamlParseException));
        var name = e.Exception.GetType().Name;
        shutdown = name != "XamlParseException" && name != "ResourceReferenceKeyNotFoundException";

        var tt = e.Exception.GetType();
        //// Process exception
        //if (e.Exception is DivideByZeroException)
        //{
        //    // Recoverable - continue processing
        //    shutdown = false;
        //}
        //else if (e.Exception is ArgumentNullException)
        //{
        //    // Unrecoverable - end processing
        //    shutdown = true;
        //}

        if (shutdown)
        {
            // If unrecoverable, attempt to save data
            var result = MessageBox.Show($"Application must exit:\n\n{e.Exception.Message}\n\nSave before exit?", "app",
                                         MessageBoxButton.YesNo, 
                                         MessageBoxImage.Error);
            if (result == MessageBoxResult.Yes)
            {
                settingsController?.Save();
                accountCollection?.Save();
                vpnCollection?.Save();
            }

            Logger.Error(e.Exception, "Unrecoverable Exception");

            // Return exit code
            Shutdown(-1);
        }

        // Prevent default unhandled exception processing
        e.Handled = true;
    }
}