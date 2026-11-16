using System.ComponentModel;
using System.Runtime.InteropServices;

namespace APThermo.Harness.ProcessOwner;

/// <summary>Native Windows job lifetime, with no inheritable handles or breakaway policy.</summary>
internal sealed class WindowsJob : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
    private delegate nint CreateJob(nint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
    private delegate nint OpenJob(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool SetInformation(nint job, int informationClass, nint information, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool QueryJob(nint job, int informationClass, nint information, uint length, out uint returned);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool AssignJob(nint job, nint process);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool TerminateJob(nint job, uint exitCode);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool CloseHandle(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint CurrentProcess();

    private readonly nint _library;
    private readonly CloseHandle _close;
    private readonly TerminateJob _terminate;
    private nint _handle;

    private WindowsJob(nint library, nint handle)
    {
        _library = library;
        _handle = handle;
        _close = Load<CloseHandle>(library, "CloseHandle");
        _terminate = Load<TerminateJob>(library, "TerminateJobObject");
    }

    internal static WindowsJob Create(string name)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
        {
            throw new PlatformNotSupportedException("the job layout requires Windows x64");
        }
        var library = NativeLibrary.Load("kernel32.dll");
        var close = Load<CloseHandle>(library, "CloseHandle");
        nint handle = 0;
        try
        {
            Marshal.SetLastPInvokeError(0);
            handle = Load<CreateJob>(library, "CreateJobObjectW")(0, name);
            var error = Marshal.GetLastPInvokeError();
            if (handle == 0 || error == 183) { throw new Win32Exception(error, "could not create a unique job"); }
            Configure(library, handle);
            return new WindowsJob(library, handle);
        }
        catch
        {
            if (handle != 0) { _ = close(handle); }
            NativeLibrary.Free(library);
            throw;
        }
    }

    private static void Configure(nint library, nint handle)
    {
        // JOBOBJECT_EXTENDED_LIMIT_INFORMATION is 144 bytes on Windows x64;
        // BasicLimitInformation.LimitFlags is the uint at offset 16.
        const int size = 144;
        var buffer = new byte[size];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 0x2000);
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(buffer, 0, pointer, size);
            if (!Load<SetInformation>(library, "SetInformationJobObject")(handle, 9, pointer, size))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not configure job");
            }
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal static void Join(string name)
    {
        var library = NativeLibrary.Load("kernel32.dll");
        try
        {
            var handle = Load<OpenJob>(library, "OpenJobObjectW")(1, false, name);
            if (handle == 0) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not open job"); }
            var closed = false;
            try
            {
                var current = Load<CurrentProcess>(library, "GetCurrentProcess")();
                if (!Load<AssignJob>(library, "AssignProcessToJobObject")(handle, current))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not join job");
                }
            }
            finally { closed = Load<CloseHandle>(library, "CloseHandle")(handle); }
            if (!closed) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not close bootstrap job handle"); }
        }
        finally { NativeLibrary.Free(library); }
    }

    internal static void StopNamed(string name)
    {
        var library = NativeLibrary.Load("kernel32.dll");
        try
        {
            var handle = Load<OpenJob>(library, "OpenJobObjectW")(8, false, name);
            if (handle == 0) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not open job for termination"); }
            try
            {
                if (!Load<TerminateJob>(library, "TerminateJobObject")(handle, 1))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not terminate job");
                }
            }
            finally { _ = Load<CloseHandle>(library, "CloseHandle")(handle); }
        }
        finally { NativeLibrary.Free(library); }
    }

    internal int ActiveProcesses()
    {
        // JOBOBJECT_BASIC_ACCOUNTING_INFORMATION is 48 bytes on Windows x64;
        // ActiveProcesses is the uint at offset 40.
        const int size = 48;
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            return !Load<QueryJob>(_library, "QueryInformationJobObject")(_handle, 1, pointer, size, out _)
                ? throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not query active job processes")
                : Marshal.ReadInt32(pointer, 40);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal void Stop()
    {
        if (!_terminate(_handle, 1)) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not terminate job"); }
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            var handle = _handle;
            _handle = 0;
            try
            {
                if (!_close(handle)) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "could not close job handle"); }
            }
            finally { NativeLibrary.Free(_library); }
        }
    }

    private static T Load<T>(nint library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
}
