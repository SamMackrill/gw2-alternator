using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using alternator.core;

namespace alternator.model
{
    public class Client
    {
        private readonly Account account;

        public Client(Account account)
        {
            this.account = account;
        }

        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hWnd);

        public async Task Start()
        {
            // Run gw2 exe with arguments
            var pi = new ProcessStartInfo(@"G:\Games\gw2\Gw2-64.exe")
            {
                CreateNoWindow = true,
                Arguments = $"-autologin -windowed -nosound -shareArchive -maploadinfo", // -dat \"{account.LoginFile}\"",
                UseShellExecute = false,
                WorkingDirectory = @"G:\Games\gw2"
            };
            var p = new Process {StartInfo = pi};
            p.EnableRaisingEvents = true;
            p.Exited += Exited;

            var success = p.Start();
            p.WaitForInputIdle();
            // Wait for minimum start
            // Remove mutex

            Thread.Sleep(new TimeSpan(0,0,20));

            _ = SetForegroundWindow(p.MainWindowHandle);

            InputSender.ClickKey(0x1c); // Enter

            await p.WaitForExitAsync();
        }

        private void Exited(object? sender, EventArgs e)
        {
            var p = sender as Process;
        }

    }
}
