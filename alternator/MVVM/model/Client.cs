using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
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
                Debug.WriteLine("");
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
            Debug.WriteLine($"{account.Name} Client Started");
            p.WaitForInputIdle();
            //_ = SetWinEventHook((uint)AccessibleEvents.SystemSound, (uint)AccessibleEvents.AcceleratorChange, IntPtr.Zero, WinEventHookCallback, (uint)p.Id, 0, 0);
            KillMutex();
            Debug.WriteLine($"{account.Name} Client Killed Mutex");
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
            Debug.WriteLine("Mutex Killed");
        }
        private bool KillMutex()
        {
            var name = "AN-Mutex-Window-Guild Wars 2";

            var handle = Win32Handles.GetHandle(p.Id, name, Win32Handles.MatchMode.EndsWith);

            if (handle != null)
            {
                Debug.WriteLine($"{account.Name} Got handle to Mutex");
                handle.Kill();
                return true;
            }

            return p.MainWindowHandle != IntPtr.Zero;
        }

        public void WaitForExit()
        {
            p.Refresh();
            var memoryUsage = p.WorkingSet64 / 1024;

            //eventCounter.DebugDisplay();
            Debug.WriteLine($"{account.Name} Wait for Character Selection");
            //while (!AllExpectedEventsToLogSelection())
            //{
            //    Thread.Sleep(20);
            //}
            //eventCounter.DebugDisplay();
            WaitForStable(ref memoryUsage, 2000, 750_000, 200);

            SendEnter();

            Debug.WriteLine($"{account.Name} Wait for load-in to world");
            WaitForStable(ref memoryUsage, 2000, 1_800_000, 2000);

            Debug.WriteLine($"{account.Name} Kill!");
            p.Kill(true);
        }

        private void WaitForStable(ref long memoryUsage, int pause, long characterSelectMinMemory, long characterSelectMinDiff)
        {
            do
            {
                Thread.Sleep(pause);
            } while (!MemoryUsageStable(ref memoryUsage, characterSelectMinMemory, characterSelectMinDiff));
        }

        private bool MemoryUsageStable(ref long lastMemoryUsage, long min, long delta)
        {
            p.Refresh();
            var memoryUsage = p.WorkingSet64 / 1024;
            var diff = Math.Abs(memoryUsage - lastMemoryUsage);
            lastMemoryUsage = memoryUsage;
            Debug.WriteLine($"{account.Name} Mem={memoryUsage} Mem-Diff={diff}");
            return memoryUsage > min && diff < delta;
        }

        private void SendEnter()
        {
            Debug.WriteLine($"{account.Name} Send ENTER");
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
                //Debug.WriteLine($"WinEventHookCallback {accessibleEvent}({eventType})");
                _ = GetClassName(wnd, eventBuffer, eventBuffer.Capacity);
                var className = eventBuffer.ToString();
                if (!string.IsNullOrEmpty(className))Debug.WriteLine($"ClassName = {className}");

                switch (accessibleEvent)
                {
                    case AccessibleEvents.NameChange:
                        {
                            _ = GetWindowText(wnd, eventBuffer, eventBuffer.Capacity);
                            var windowsText = eventBuffer.ToString();
                            //Debug.WriteLine($"NameChange to {windowsText}");
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
                Debug.WriteLine(e);
            }

        }

        private void Exited(object? sender, EventArgs e)
        {
            var p = sender as Process;
        }

    }
}
