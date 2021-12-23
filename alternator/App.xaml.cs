global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows;
global using System.Windows.Data;
global using System.Windows.Controls;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Collections.ObjectModel;
global using System.Collections.Specialized;
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
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}