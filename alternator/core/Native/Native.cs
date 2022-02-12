namespace guildwars2.tools.alternator;

[Flags]
public enum DuplicateOptions : uint
{
    DUPLICATE_CLOSE_SOURCE = (0x00000001),// Closes the source handle. This occurs regardless of any error status returned.
    DUPLICATE_SAME_ACCESS = (0x00000002), //Ignores the dwDesiredAccess parameter. The duplicate handle has the same access as the source handle.
}

public enum SYSTEM_INFORMATION_CLASS
{
    SystemHandleInformation = 0x0010,
    SystemExtendedHandleInformation = 0x0040,
}

public enum NtStatus : uint
{
    Success = 0x00000000,

    Warning = 0x80000000,
    NoMoreEntries = 0x8000001a,

    Error = 0xc0000000,
    InvalidInfoClass = 0xC0000003,
    InfoLengthMismatch = 0xc0000004,
    BufferOverflow = 0x80000005,
}

public enum ObjectInformationClass : int
{
    ObjectBasicInformation = 0,
    ObjectNameInformation = 1,
    ObjectTypeInformation = 2,
    ObjectAllTypesInformation = 3,
    ObjectHandleInformation = 4
}

[Flags]
public enum ProcessAccessFlags : uint
{
    All = 0x001F0FFF,
    Terminate = 0x00000001,
    CreateThread = 0x00000002,
    VirtualMemoryOperation = 0x00000008,
    VirtualMemoryRead = 0x00000010,
    VirtualMemoryWrite = 0x00000020,
    DuplicateHandle = 0x00000040,
    CreateProcess = 0x000000080,
    SetQuota = 0x00000100,
    SetInformation = 0x00000200,
    QueryInformation = 0x00000400,
    QueryLimitedInformation = 0x00001000,
    Synchronize = 0x00100000
}

[StructLayout(LayoutKind.Sequential)]
public struct OBJECT_BASIC_INFORMATION
{ // Information Class 0
    public int Attributes;
    public int GrantedAccess;
    public int HandleCount;
    public int PointerCount;
    public int PagedPoolUsage;
    public int NonPagedPoolUsage;
    public int Reserved1;
    public int Reserved2;
    public int Reserved3;
    public int NameInformationLength;
    public int TypeInformationLength;
    public int SecurityDescriptorLength;
    public System.Runtime.InteropServices.ComTypes.FILETIME CreateTime;
}

[StructLayout(LayoutKind.Sequential)]
public struct OBJECT_NAME_INFORMATION
{ // Information Class 1
    public UNICODE_STRING Name;
}

[StructLayout(LayoutKind.Sequential)]
public struct UNICODE_STRING
{
    public UNICODE_STRING(string s)
    {
        Length = (ushort)(s.Length * 2);
        MaximumLength = (ushort)(Length + 2);
        Buffer = Marshal.StringToHGlobalUni(s);
    }

    public ushort Length;
    public ushort MaximumLength;
    public IntPtr Buffer;

    public void Free()
    {
        if (Buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Buffer);
            Buffer = IntPtr.Zero;
            Length = 0;
        }
    }

    public override string ToString()
    {
        return Length > 0 ? Marshal.PtrToStringUni(Buffer, Length >> 1) : string.Empty;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX
{
    public IntPtr Object;
    public UIntPtr UniqueProcessId;
    public UIntPtr HandleValue;
    public uint GrantedAccess;
    public ushort CreatorBackTraceIndex;
    public ushort ObjectTypeIndex;
    public uint HandleAttributes;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential)]
public struct SYSTEM_HANDLE_TABLE_ENTRY_INFO
{
    public ushort UniqueProcessId;
    public ushort CreatorBackTraceIndex;
    public byte ObjectTypeIndex;
    public byte HandleAttributes;
    public ushort HandleValue;
    public IntPtr Object;
    public uint GrantedAccess;
}

public enum ShowWindowCommands : uint
{
    /// <summary>
    /// Hides the window and activates another window.
    /// </summary>
    Hide = 0,
    /// <summary>
    /// Activates and displays a window. If the window is minimized or
    /// maximized, the system restores it to its original size and position.
    /// An application should specify this flag when displaying the window
    /// for the first time.
    /// </summary>
    ShowNormal = 1,
    /// <summary>
    /// Activates the window and displays it as a minimized window.
    /// </summary>
    ShowMinimized = 2,
    /// <summary>
    /// Activates the window and displays it as a maximized window.
    /// </summary>      
    ShowMaximized = 3,
    /// <summary>
    /// Displays a window in its most recent size and position. This value
    /// is similar to <see cref="Win32.ShowWindowCommand.Normal"/>, except
    /// the window is not activated.
    /// </summary>
    ShowNoActivate = 4,
    /// <summary>
    /// Activates the window and displays it in its current size and position.
    /// </summary>
    Show = 5,
    /// <summary>
    /// Minimizes the specified window and activates the next top-level
    /// window in the Z order.
    /// </summary>
    Minimize = 6,
    /// <summary>
    /// Displays the window as a minimized window. This value is similar to
    /// <see cref="Win32.ShowWindowCommand.ShowMinimized"/>, except the
    /// window is not activated.
    /// </summary>
    ShowMinNoActive = 7,
    /// <summary>
    /// Displays the window in its current size and position. This value is
    /// similar to <see cref="Win32.ShowWindowCommand.Show"/>, except the
    /// window is not activated.
    /// </summary>
    ShowNA = 8,
    /// <summary>
    /// Activates and displays the window. If the window is minimized or
    /// maximized, the system restores it to its original size and position.
    /// An application should specify this flag when restoring a minimized window.
    /// </summary>
    Restore = 9,
    /// <summary>
    /// Sets the show state based on the SW_* value specified in the
    /// STARTUPINFO structure passed to the CreateProcess function by the
    /// program that started the application.
    /// </summary>
    ShowDefault = 10,
    /// <summary>
    ///  <b>Windows 2000/XP:</b> Minimizes a window, even if the thread
    /// that owns the window is not responding. This flag should only be
    /// used when minimizing windows from a different thread.
    /// </summary>
    ForceMinimize = 11
}
