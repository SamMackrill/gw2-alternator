using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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


        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);


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
            var p = new Process { StartInfo = pi };
            p.EnableRaisingEvents = true;
            p.Exited += Exited;

            _ = p.Start();
            _ = SetWinEventHook(0x8000, 0x800C, IntPtr.Zero, WinEventHookCallback, (uint)p.Id, 0, 0);
            p.WaitForInputIdle();
            // Wait for minimum start
            // Remove mutex

            await Task.Delay(new TimeSpan(0, 0, 20));

            _ = SetForegroundWindow(p.MainWindowHandle);
            InputSender.ClickKey(0x1c); // Enter

            await p.WaitForExitAsync();
        }

        //private static readonly string DX_WINDOW_CLASSNAME = "ArenaNet_Dx_Window_Class"; //gw2 and gw1 main game window
        //private static readonly string DX_WINDOW_CLASSNAME_DX11BETA = "ArenaNet_Gr_Window_Class"; //gw2 dx11 main game window
        //private static readonly string DIALOG_WINDOW_CLASSNAME = "ArenaNet_Dialog_Class"; //gw1 patcher
        //private static readonly string LAUNCHER_WINDOW_CLASSNAME = "ArenaNet"; //gw2 launcher


        private void WinEventHookCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            return;

            try
            {
                StringBuilder eventBuffer = new(100);
                var accessibleEvent = (AccessibleEvents)eventType;
                Debug.WriteLine($"WinEventHookCallback {accessibleEvent}({eventType})");
                switch (accessibleEvent)
                {
                    case AccessibleEvents.NameChange:
                        {
                            _ = GetWindowText(hwnd, eventBuffer, eventBuffer.Capacity);
                            var windowsText = eventBuffer.ToString();
                            Debug.WriteLine($"NameChange to {windowsText}");
                            break;
                        }
                    case AccessibleEvents.Create:
                        try
                        {

                            //_ = GetClassName(hwnd, eventBuffer, eventBuffer.Capacity + 1);

                            //var className = eventBuffer.ToString();
                            //if (className.Equals(LAUNCHER_WINDOW_CLASSNAME))
                            //{
                            //    // Launcher
                            //}
                            //else if (className.Equals(DIALOG_WINDOW_CLASSNAME))
                            //{
                            //    // Patcher Dialog
                            //}
                            //else if (className.Equals(DX_WINDOW_CLASSNAME_DX11BETA) || className.Equals(DX_WINDOW_CLASSNAME))
                            //{
                            //    // Main Window
                            //}

                        }
                        catch { }

                        break;
                    case AccessibleEvents.Show:

                        break;
                    default:

                        return;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw;
            }

        }

        private void Exited(object? sender, EventArgs e)
        {
            var p = sender as Process;
        }

    }
}
