using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace guildwars2.tools.alternator
{
    public class Client
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly Account account;

        public Client(Account account)
        {
            this.account = account;
        }

        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr modWinEventProc, WinEventDelegate winEventProc, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr wnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr wnd, StringBuilder text, int maxCount);

        private Process p;

        private class EventCounter
        {
            public int NameChange { get; set; }
            public int Gw2NameChange { get; set; }
            public int Create { get; set; }
            public int Destroy { get; set; }
            public int Show { get; set; }
            public int LocationChange { get; set; }
            public int Focus { get; set; }

            public void DebugDisplay()
            {
                Debug.WriteLine($"NameChange: {NameChange}");
                Debug.WriteLine($"Gw2NameChange: {Gw2NameChange}");
                Debug.WriteLine($"Create: {Create}");
                Debug.WriteLine($"Destroy: {Destroy}");
                Debug.WriteLine($"Show: {Show}");
                Debug.WriteLine($"LocationChange: {LocationChange}");
                Debug.WriteLine($"Focus: {Focus}");
                Logger.Debug("");
            }
        }

        private readonly EventCounter eventCounter = new EventCounter();

        public void Start()
        {
            // Run gw2 exe with arguments
            var pi = new ProcessStartInfo(@"G:\Games\gw2\Gw2-64.exe")
            {
                CreateNoWindow = true,
                Arguments = $"-autologin -windowed -nosound -shareArchive -maploadinfo", // -dat \"{account.LoginFile}\"",
                UseShellExecute = false,
                WorkingDirectory = @"G:\Games\gw2"
            };
            p = new Process { StartInfo = pi };
            p.EnableRaisingEvents = true;
            p.Exited += Exited;

            _ = p.Start();
            Logger.Debug("{0} Client Started", account.Name);
            p.WaitForInputIdle();
            _ = SetWinEventHook((uint)AccessibleEvents.SystemSound, (uint)AccessibleEvents.AcceleratorChange, IntPtr.Zero, WinEventHookCallback, (uint)p.Id, 0, 0);
            KillMutex();
            Logger.Debug("{0} Client Killed Mutex", account.Name);
        }

        public async Task StartAsync()
        {
            // Run gw2 exe with arguments
            var pi = new ProcessStartInfo(@"G:\Games\gw2\Gw2-64.exe")
            {
                CreateNoWindow = true,
                Arguments = $"-autologin -windowed -nosound -shareArchive -maploadinfo", // -dat \"{account.LoginFile}\"",
                UseShellExecute = false,
                WorkingDirectory = @"G:\Games\gw2"
            };
            p = new Process { StartInfo = pi };
            p.EnableRaisingEvents = true;
            p.Exited += Exited;

            _ = p.Start();
            p.WaitForInputIdle();
            _ = SetWinEventHook((uint)AccessibleEvents.SystemSound, (uint)AccessibleEvents.AcceleratorChange, IntPtr.Zero, WinEventHookCallback, (uint)p.Id, 0, 0);
            KillMutex();
            Logger.Debug("Mutex Killed");
        }
        private bool KillMutex()
        {
            var name = "AN-Mutex-Window-Guild Wars 2";

            var handle = Win32Handles.GetHandle(p.Id, name, Win32Handles.MatchMode.EndsWith);

            if (handle != null)
            {
                Logger.Debug("{0} Got handle to Mutex", account.Name);
                handle.Kill();
                return true;
            }

            return p.MainWindowHandle != IntPtr.Zero;
        }

        public bool WaitForExit()
        {
            try
            {
                p.Refresh();
                if (p.HasExited)
                {
                    Logger.Debug("{0} Died!", account.Name);
                    return false;
                }

                var memoryUsage = p.WorkingSet64 / 1024;

                //eventCounter.DebugDisplay();
                Logger.Debug("{0} Wait for Character Selection", account.Name);
                //while (!AllExpectedEventsToLogSelection())
                //{
                //    Thread.Sleep(20);
                //}
                //eventCounter.DebugDisplay();

                if (!WaitForStable(ref memoryUsage, 2000, 750_000, 200, 120))
                {
                    Logger.Debug("{0} Timed-out waiting for Character Selection", account.Name);
                    return false;
                }
                if (p.HasExited)
                {
                    Logger.Debug("{0} Died!", account.Name);
                    return false;
                }

                SendEnter();

                Logger.Debug("{0} Wait for load-in to world", account.Name);
                if (!WaitForStable(ref memoryUsage, 2000, 900_000, 2000, 180))
                {
                    Logger.Debug("{0} Timed-out waiting for load-in to world", account.Name);
                    return false;
                }
                if (p.HasExited)
                {
                    Logger.Debug("{0} Died!", account.Name);
                    return false;
                }
                Logger.Debug("{0} Kill!", account.Name);
                p.Kill(true);
                return true;
            }
            catch (Exception e)
            {
                Logger.Debug(e, "{0} Failed", account.Name);
                return false;
            }
            finally
            {
                p.Refresh();
                if (!p.HasExited)
                {
                    Logger.Debug("{0} Kill runaway", account.Name);
                    p.Kill(true);
                }
            }
        }

        private bool WaitForStable(ref long memoryUsage, int pause, long characterSelectMinMemory, long characterSelectMinDiff, double timeout)
        {
            var start = DateTime.Now;
            do
            {
                Thread.Sleep(pause);
                if (MemoryUsageStable(ref memoryUsage, characterSelectMinMemory, characterSelectMinDiff)) return true;
            } while (DateTime.Now.Subtract(start).TotalSeconds < timeout) ;
            return false;
        }

        private bool MemoryUsageStable(ref long lastMemoryUsage, long min, long delta)
        {
            p.Refresh();
            if (p.HasExited) return true;
            var memoryUsage = p.WorkingSet64 / 1024;
            var diff = Math.Abs(memoryUsage - lastMemoryUsage);
            lastMemoryUsage = memoryUsage;
            Logger.Debug("{0} Mem={1} Mem-Diff={2}", account.Name, memoryUsage, diff);
            return memoryUsage > min && diff < delta;
        }

        private void SendEnter()
        {
            Logger.Debug("{0} Send ENTER", account.Name);
            var currentFocus = GetForegroundWindow();
            _ = SetForegroundWindow(p.MainWindowHandle);
            InputSender.ClickKey(0x1c); // Enter
            _ = SetForegroundWindow(currentFocus);
        }

        private bool AllExpectedEventsToLogSelection()
        {
            return eventCounter.Gw2NameChange > 0 && eventCounter.Focus > 0 && eventCounter.Show>0;
        }


        private void WinEventHookCallback(IntPtr hWinEventHook, uint eventType, IntPtr wnd, int idObject, int idChild, uint dwEventThread, uint eventTime)
        {
            try
            {
                StringBuilder eventBuffer = new(100);
                var accessibleEvent = (AccessibleEvents)eventType;
                Logger.Debug("{0} {1}({2})", account.Name, accessibleEvent, eventType);
                _ = GetClassName(wnd, eventBuffer, eventBuffer.Capacity);
                var className = eventBuffer.ToString();
                if (!string.IsNullOrEmpty(className)) Logger.Debug("{0} ClassName={1}", account.Name, className);

                switch (accessibleEvent)
                {
                    case AccessibleEvents.NameChange:
                        {
                            _ = GetWindowText(wnd, eventBuffer, eventBuffer.Capacity);
                            var windowsText = eventBuffer.ToString();
                            Logger.Debug("{0} NameChange to {1}", account.Name, windowsText);
                            eventCounter.NameChange++;
                            if (windowsText == "Guild Wars 2") eventCounter.Gw2NameChange++;
                            break;
                        }
                    case AccessibleEvents.Create:
                        eventCounter.Create++;
                        break;
                    case AccessibleEvents.Destroy:
                        eventCounter.Destroy++;
                        break;
                    case AccessibleEvents.Show:
                        eventCounter.Show++;
                        break;
                    case AccessibleEvents.Focus:
                        eventCounter.Focus++;
                        break;
                    case AccessibleEvents.LocationChange:
                        eventCounter.LocationChange++;
                        break;
                    default:
                        return;
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e, "{0} Failed", account.Name);
            }

        }

        private void Exited(object? sender, EventArgs e)
        {
            var p = sender as Process;
        }

    }
}
