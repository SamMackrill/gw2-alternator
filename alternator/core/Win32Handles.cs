namespace guildwars2.tools.alternator;

static class Win32Handles
{
    public enum MatchMode : byte
    {
        Contains = 0,
        Exact = 1,
        StartsWith = 2,
        EndsWith = 3,
    }

    private const byte THREADS = 4;

    public interface IObjectHandle
    {
        void Kill();
    }

    private class ObjectHandle : IObjectHandle
    {
        protected UIntPtr processId, handle;

        public ObjectHandle(UIntPtr processId, UIntPtr handle)
        {
            this.processId = processId;
            this.handle = handle;
        }

        public void Kill()
        {
            using var p = Process.GetProcessById((int)processId.GetValue());
            var h = IntPtr.Zero;
            try
            {
                if (!Native.DuplicateHandle(p.Handle, handle, Native.GetCurrentProcess(), out h, 0, false, DuplicateOptions.DUPLICATE_CLOSE_SOURCE))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                if (h != IntPtr.Zero)
                    Native.CloseHandle(h);
            }
        }
    }

    private class Buffer : IDisposable
    {
        public Buffer(int length)
        {
            handle = Marshal.AllocHGlobal(this.length = length);
        }

        public IntPtr handle;
        public int length;

        public void ReAlloc(int length)
        {
            handle = Marshal.ReAllocHGlobal(handle, (IntPtr)(this.length = length));
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    private static string? GetObjectName(Buffer buffer, UIntPtr processId, UIntPtr handle, bool queryInfo)
    {
        var _processHandle = Native.OpenProcess(ProcessAccessFlags.All, false, processId);
        var _handle = IntPtr.Zero;

        try
        {
            if (!Native.DuplicateHandle(_processHandle, handle, Native.GetCurrentProcess(), out _handle, 0, false, DuplicateOptions.DUPLICATE_SAME_ACCESS))
                return null;

            int nameLength;

            if (queryInfo)
            {
                OBJECT_BASIC_INFORMATION basicInfo;
                nameLength = 0;

                //warning: not fully implemented in Wine and will always return a 0 length

                if (Native.NtQueryObject(_handle, ObjectInformationClass.ObjectBasicInformation, out basicInfo, Marshal.SizeOf(typeof(OBJECT_BASIC_INFORMATION)), ref nameLength) != NtStatus.Success)
                    return null;

                nameLength = basicInfo.NameInformationLength;

                if (nameLength > buffer.length)
                {
                    buffer.ReAlloc(nameLength);
                }
                else if (nameLength == 0)
                {
                    return null;
                }
            }
            else
            {
                nameLength = buffer.length;
            }

            var querying = true;

            do
            {
                var r = Native.NtQueryObject(_handle, ObjectInformationClass.ObjectNameInformation, buffer.handle, buffer.length, ref nameLength);

                switch (r)
                {
                    case NtStatus.Success:

                        querying = false;

                        break;
                    case NtStatus.InvalidInfoClass:
                    case NtStatus.BufferOverflow:

                        if (nameLength > buffer.length)
                            buffer.ReAlloc(nameLength);
                        else
                            return null;

                        break;
                    default:

                        return null;
                }
            }
            while (querying);

            var nameInfo = (OBJECT_NAME_INFORMATION)Marshal.PtrToStructure(buffer.handle, typeof(OBJECT_NAME_INFORMATION))!;

            try
            {
                if (nameInfo.Name.Length == 0)
                    return null;
                return nameInfo.Name.ToString();
            }
            catch (Exception)
            {
                //Util.Logging.Log(e);
            }

            return null;
        }
        finally
        {
            if (_handle != IntPtr.Zero)
                Native.CloseHandle(_handle);
            if (_processHandle != IntPtr.Zero)
                Native.CloseHandle(_processHandle);
        }
    }

    public static IObjectHandle? GetHandle(int processId, string objectName, MatchMode match)
    {
        Func<string, bool> f;

        switch (match)
        {
            case MatchMode.Contains:
                f = s => s.IndexOf(objectName) != -1;
                break;
            case MatchMode.EndsWith:
                f = s => s.EndsWith(objectName, StringComparison.Ordinal);
                break;
            case MatchMode.Exact:
                f = s => s.Equals(objectName);
                break;
            case MatchMode.StartsWith:
                f = s => s.StartsWith(objectName, StringComparison.Ordinal);
                break;
            default:
                throw new NotSupportedException();
        }

        return GetHandle(processId, objectName, f);
    }

    public static IObjectHandle? GetHandle(int processId, string objectName, Func<string, bool> objectNameCallback)
    {
        var infoClass = SYSTEM_INFORMATION_CLASS.SystemExtendedHandleInformation;
        var infoLength = 0x10000;
        var _processId = (UIntPtr)processId;
        Buffer[]? buffers = null;

        var _info = Marshal.AllocHGlobal(infoLength);

        try
        {
            //CNST_SYSTEM_HANDLE_INFORMATION is limited to 16-bit process IDs
            var querying = true;
            var length = 0;

            do
            {
                var r = Native.NtQuerySystemInformation(infoClass, _info, infoLength, ref length);

                switch (r)
                {
                    case NtStatus.Success:

                        querying = false;

                        break;
                    case NtStatus.InfoLengthMismatch:

                        _info = Marshal.ReAllocHGlobal(_info, (IntPtr)(infoLength = length));

                        break;
                    case NtStatus.InvalidInfoClass:

                        if (infoClass == SYSTEM_INFORMATION_CLASS.SystemExtendedHandleInformation)
                        {
                            infoClass = SYSTEM_INFORMATION_CLASS.SystemHandleInformation;
                        }
                        else
                        {
                            throw new NotSupportedException("SystemHandleInformation not supported");
                        }

                        break;
                    default:

                        throw new Exception(r.ToString("x"));
                }
            }
            while (querying);

            IntPtr handle;
            long handleCount;
            Type infoType;

            if (infoClass == SYSTEM_INFORMATION_CLASS.SystemExtendedHandleInformation)
            {
#if x86
                    handleCount = Marshal.ReadInt32(_info);
#elif x64
                    handleCount = Marshal.ReadInt64(_info);
#else
                if (IntPtr.Size == 4)
                    handleCount = Marshal.ReadInt32(_info);
                else
                    handleCount = Marshal.ReadInt64(_info);
#endif
                handle = _info + IntPtr.Size * 2;
                infoType = typeof(SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX);
            }
            else
            {
                //SystemHandleInformation count is 32bit
                handleCount = Marshal.ReadInt32(_info);

                handle = _info + IntPtr.Size;
                infoType = typeof(SYSTEM_HANDLE_TABLE_ENTRY_INFO);
            }

            var infoSize = Marshal.SizeOf(infoType);

            buffers = new Buffer[THREADS];

            for (var j = 0; j < THREADS; j++)
            {
                buffers[j] = new Buffer(256);
            }

            var canQuery = infoClass == SYSTEM_INFORMATION_CLASS.SystemExtendedHandleInformation;

            //warning: NtQueryObject can cause a deadlock when querying an item that is waiting

            var loop = Loop.For(0, handleCount, THREADS, 1000,
                delegate(byte thread, long i, Loop.IState state)
                {
                    var handleInfo = Marshal.PtrToStructure((IntPtr)(handle.GetValue() + i * infoSize), infoType);
                        
                    UIntPtr pid, h;

                    if (infoClass == SYSTEM_INFORMATION_CLASS.SystemExtendedHandleInformation)
                    {
                        var info = (SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX)handleInfo!;

                        pid = info.UniqueProcessId;
                        h = info.HandleValue;
                    }
                    else
                    {
                        var info = (SYSTEM_HANDLE_TABLE_ENTRY_INFO)handleInfo!;

                        //try to recover 32-bit processId from 16-bit info
                        if (info.UniqueProcessId == (ushort)processId)
                            pid = (UIntPtr)processId;
                        else
                            pid = (UIntPtr)info.UniqueProcessId;
                        h = (UIntPtr)info.HandleValue;
                    }

                    if (processId > 0 && pid != _processId)
                        return;

                    var name = GetObjectName(buffers[thread], pid, h, canQuery);

                    if (name != null && objectNameCallback(name))
                    {
                        state.Return(new ObjectHandle(pid, h));
                    }
                });

            loop.Wait();

            return loop.Result as ObjectHandle;
        }
        finally
        {
            Marshal.FreeHGlobal(_info);
            if (buffers != null)
            {
                foreach (var b in buffers)
                {
                    using (b) { }
                }
            }
        }
    }
}