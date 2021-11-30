using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using alternator.core;
using alternator.model;

namespace alternator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var accounts = new List<Account> { new("Fish2", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\4.dat")))};
            var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")));

            launcher.Launch(accounts);
        }
    }
}
