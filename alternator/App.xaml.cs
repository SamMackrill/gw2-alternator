global using System;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using System.ComponentModel;

global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;

global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Collections.Specialized;

global using System.Windows;
global using System.Windows.Data;
global using System.Windows.Controls;
global using System.Windows.Threading;

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

    protected override void OnStartup(StartupEventArgs e)
    {
        SetLogging();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogManager.Shutdown();
        base.OnExit(e);
    }

    private void SetLogging()
    {
        var config = new NLog.Config.LoggingConfiguration();

        // Targets where to log to: File and Console
        var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "alternator-log.txt", DeleteOldFileOnStartup = true };
        var debugLogfile = new NLog.Targets.FileTarget("debuglogfile") { FileName = "alternator-debug-log.txt", DeleteOldFileOnStartup = true };
        var errorLogfile = new NLog.Targets.FileTarget("errorlogfile") { FileName = "alternator-error-log.txt" };
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
        var shutdown = true;

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
                // Save data
            }

            Logger.Error(e.Exception, "Unrecoverable Exception");

            // Return exit code
            Shutdown(-1);
        }

        // Prevent default unhandled exception processing
        e.Handled = true;
    }
}