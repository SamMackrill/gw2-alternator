﻿global using System;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Text.RegularExpressions;
global using System.ComponentModel;
global using System.Xaml;
global using System.Globalization;
global using System.Reflection;

global using System.Threading;
global using System.Threading.Tasks;

global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;

global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Collections.Specialized;
global using System.Collections.Concurrent;

global using System.Windows;
global using System.Windows.Threading;
global using System.Windows.Input;
global using System.Windows.Markup;
global using System.Windows.Media;

global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Runtime.InteropServices.ComTypes;

global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
global using CommunityToolkit.Mvvm.DependencyInjection;

global using Microsoft.Extensions.DependencyInjection;

global using AsyncAwaitBestPractices;

global using MvvmDialogs;
global using MvvmDialogs.FrameworkDialogs.FolderBrowser;

global using guildwars2.tools.alternator.MVVM.model;
global using guildwars2.tools.alternator.MVVM.view;
global using guildwars2.tools.alternator.MVVM.viewmodel;

global using Gw2Sharp.WebApi.V2.Clients;
global using Gw2Sharp.WebApi.V2.Models;

global using NLog;
global using NLog.Config;
global using NLog.Layouts;
global using NLog.Targets;

global using File = System.IO.File;

using XamlParseException = System.Windows.Markup.XamlParseException;


namespace guildwars2.tools.alternator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string ApplicationName => Assembly.GetExecutingAssembly().GetName().Name ?? "gw2-alternator";
    public static string ApplicationFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationName);

    public App()
    {
        serviceCollection = new ServiceCollection();
    }

    private readonly ServiceCollection serviceCollection;

    protected override void OnStartup(StartupEventArgs e)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var applicationFolder = new DirectoryInfo(ApplicationFolder);
        if (!applicationFolder.Exists) applicationFolder.Create();
        SetLogging(applicationFolder);


        serviceCollection.AddSingleton<IDialogService, DialogService>();
        serviceCollection.AddSingleton<ISettingsController, SettingsController>(_ =>
        {
            var settingsController = new SettingsController(applicationFolder)
            {
                DatFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"Local.dat")),
                GfxSettingsFile = new FileInfo(Path.Combine(appData, @"Guild Wars 2", @"GFXSettings.Gw2-64.exe.xml"))
            };
            settingsController.DiscoverGw2ExeLocation();
            return settingsController;
        });

        serviceCollection.AddSingleton<IAccountCollection, AccountCollection>(_ =>
        {
            var launchbuddyFolder = Path.Combine(appData, @"Gw2 Launchbuddy");
            Logger.Info("LaunchBuddy Folder: {0} Exists={1}", launchbuddyFolder, Directory.Exists(launchbuddyFolder));
            var launcherFolder = Path.Combine(appData, @"Gw2Launcher");
            Logger.Info("GW2Launcher Folder: {0} Exists={1}", launcherFolder, Directory.Exists(launcherFolder));
            return new AccountCollection(applicationFolder, launchbuddyFolder, launcherFolder);
        });
        serviceCollection.AddSingleton<IVpnCollection, VpnCollection>(_ => new VpnCollection(applicationFolder));
        serviceCollection.AddTransient<MainViewModel>();

        Ioc.Default.ConfigureServices(serviceCollection.BuildServiceProvider());

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var settingsController = Ioc.Default.GetService<ISettingsController>();
        settingsController?.Save();
        LogManager.Shutdown();
        base.OnExit(e);
    }


    private void SetLogging(DirectoryInfo folder)
    {
        LogManager.Configuration = LoggingConfiguration(folder);
    }

    private static LoggingConfiguration LoggingConfiguration(DirectoryInfo folder)
    {
        var layout = new SimpleLayout {Text = "${longdate}|${level:uppercase=true}|${logger:shortName=true}|${message:withexception=true}" };

        var config = new LoggingConfiguration();

        var logfile = new FileTarget("logfile")
        {
            FileName = Path.Combine(folder.FullName, "gw2-alternator-log.txt"),
            Layout = layout,
            ArchiveOldFileOnStartup = true,
            ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
            MaxArchiveDays = 7,
        };
        config.AddRule(LogLevel.Info, LogLevel.Info, logfile);

        var debugLogfile = new FileTarget("debuglogfile")
        {
            FileName = Path.Combine(folder.FullName, "gw2-alternator-debug-log.txt"),
            Layout = layout,
            ArchiveOldFileOnStartup = true,
            KeepFileOpen = false,
            ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
            MaxArchiveDays = 7,
        };
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, debugLogfile);

        var errorLogfile = new FileTarget("errorlogfile")
        {
            FileName = Path.Combine(folder.FullName, "gw2-alternator-error-log.txt"),
            Layout = layout,
            ArchiveOldFileOnStartup = true,
            ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
            MaxArchiveDays = 30,
        };
        config.AddRule(LogLevel.Error, LogLevel.Fatal, errorLogfile);

        config.AddRule(LogLevel.Trace, LogLevel.Fatal, new ConsoleTarget("logconsole") { Layout = layout });

        config.AddRule(LogLevel.Trace, LogLevel.Fatal, new DebuggerTarget("debugconsole") { Layout = layout});
        return config;
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
            var dialogService = Ioc.Default.GetService<IDialogService>();

            var showerVM = new MessageShowerViewModel();
            var showerView = new MessageShowerView{DataContext = showerVM };
            showerView.Show();
            var result = dialogService?.ShowMessageBox(
                showerVM,
                $"Application must exit:\n\n{e.Exception.Message}\n\nSave before exit?",
                ApplicationName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result is null or MessageBoxResult.Yes)
            {
                var settingsController = Ioc.Default.GetService<ISettingsController>();
                settingsController?.Save();

                var accountCollection = Ioc.Default.GetService<IAccountCollection>();
                accountCollection?.Save();

                var vpnCollection = Ioc.Default.GetService<IVpnCollection>();
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