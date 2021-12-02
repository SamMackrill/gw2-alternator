using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using alternator.core;
using alternator.model;

namespace alternator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var accounts = new List<Account> {
                new("Fish2", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\4.dat"))),
                new("Fish3", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\5.dat"))),
                new("Fish4", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\6.dat"))),
                new("Fish5", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\7.dat"))),
                new("Fish6", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\8.dat"))),
                new("Fish7", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\9.dat"))),
                new("Fish8", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\10.dat"))),
                new("Fish9", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\11.dat"))),
                new("Fish10", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\12.dat"))),
                new("Fish11", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\13.dat"))),
                new("Fish12", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\14.dat"))),
                new("Fish13", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\15.dat"))),
                new("Fish14", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\16.dat"))),
                new("Fish15", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\17.dat"))),
                new("Fish16", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\18.dat"))),
                new("Fish17", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\19.dat"))),
                new("Fish18", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\20.dat"))),
                new("Fish19", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\21.dat"))),
                new("Fish20", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\22.dat"))),
                new("Fish21", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\23.dat"))),
                new("Fish22", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\24.dat"))),
                new("Fish23", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\25.dat"))),
                new("Fish24", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\26.dat"))),
                new("Fish25", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\27.dat"))),
                new("Fish26", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\28.dat"))),
                new("Fish27", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\29.dat"))),
                new("Fish28", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\30.dat"))),
                new("Fish29", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\31.dat"))),
                new("Fish30", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\32.dat"))),
                new("Fish31", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\33.dat"))),
                new("Fish32", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\34.dat"))),
                new("Fish33", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\35.dat"))),
                new("Fish34", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\36.dat"))),
                new("Fish35", new FileInfo(Path.Combine(appData, @"Gw2 Launchbuddy\Loginfiles\37.dat"))),
            };
            var launcher = new ClientController(new FileInfo(Path.Combine(appData, @"Guild Wars 2\Local.dat")));

            launcher.Launch(accounts);
        }
    }
}
