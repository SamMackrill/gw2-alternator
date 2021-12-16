namespace guildwars2.tools.alternator;

/// <summary>
/// Thanks to Bojidar Qnkov for this approach
/// https://www.codeproject.com/Articles/5264831/How-to-Send-Inputs-using-Csharp
/// </summary>
public class InputSender
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HardwareInput
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
        [FieldOffset(0)] public HardwareInput hi;
    }

    public struct Input
    {
        public int Type;
        public InputUnion Union;
    }

    [Flags]
    public enum InputType
    {
        Mouse = 0,
        Keyboard = 1,
        Hardware = 2
    }

    [Flags]
    public enum KeyEventF
    {
        KeyDown = 0x0000,
        ExtendedKey = 0x0001,
        KeyUp = 0x0002,
        Unicode = 0x0004,
        Scancode = 0x0008
    }

    [Flags]
    public enum MouseEventF
    {
        Absolute = 0x8000,
        HWheel = 0x01000,
        Move = 0x0001,
        MoveNoCoalesce = 0x2000,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010,
        MiddleDown = 0x0020,
        MiddleUp = 0x0040,
        VirtualDesk = 0x4000,
        Wheel = 0x0800,
        XDown = 0x0080,
        XUp = 0x0100
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    public static Point GetCursorPosition()
    {
        GetCursorPos(out var point);
        return point;
    }

    public static void SetCursorPosition(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void SendKeyboardInput(KeyboardInput[] kbInputs)
    {
        var inputs = new Input[kbInputs.Length];

        for (var i = 0; i < kbInputs.Length; i++)
        {
            inputs[i] = new Input
            {
                Type = (int)InputType.Keyboard,
                Union = new InputUnion
                {
                    ki = kbInputs[i]
                }
            };
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
    }

    public static void ClickKey(ushort scanCode)
    {
        var inputs = new[]
        {
            new KeyboardInput
            {
                wScan = scanCode,
                dwFlags = (uint)(KeyEventF.KeyDown | KeyEventF.Scancode),
                dwExtraInfo = GetMessageExtraInfo()
            },
            new KeyboardInput
            {
                wScan = scanCode,
                dwFlags = (uint)(KeyEventF.KeyUp | KeyEventF.Scancode),
                dwExtraInfo = GetMessageExtraInfo()
            }
        };
        SendKeyboardInput(inputs);
    }

    public static void SendMouseInput(MouseInput[] mInputs)
    {
        var inputs = new Input[mInputs.Length];

        for (var i = 0; i < mInputs.Length; i++)
        {
            inputs[i] = new Input
            {
                Type = (int)InputType.Mouse,
                Union = new InputUnion
                {
                    mi = mInputs[i]
                }
            };
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
    }
}