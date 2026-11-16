using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace APThermo.Harness.ProcessOwner;

/// <summary>A retained Linux proc-directory identity, never reopened by numeric PID.</summary>
internal sealed class LinuxProcessIdentity : IDisposable
{
    private const int DirectoryFlags = 0x10000 | 0x80000;
    private const int CloseOnExec = 0x80000;
    private const int NoSuchProcess = 3;
    private const long PidfdSendSignal = 424;
    private readonly nint _library;
    private readonly SafeFileHandle _directory;
    private readonly OpenRelative _openAt;
    private readonly Signal _signal;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
    private delegate int OpenPath(string path, int flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
    private delegate int OpenRelative(int directory, string path, int flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    private delegate long Signal(long call, int descriptor, int signal, nint information, uint flags);

    private LinuxProcessIdentity(nint library, SafeFileHandle directory)
    {
        _library = library;
        _directory = directory;
        _openAt = Load<OpenRelative>(library, "openat");
        _signal = Load<Signal>(library, "syscall");
    }

    internal static LinuxProcessIdentity Open(int pid) => OpenPathIdentity("/proc/" + pid.ToString(CultureInfo.InvariantCulture));

    internal static LinuxProcessIdentity OpenSelf() => OpenPathIdentity("/proc/self");

    private static LinuxProcessIdentity OpenPathIdentity(string path)
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("safe Linux process identity requires Linux x64");
        }
        var library = NativeLibrary.Load("libc.so.6");
        try
        {
            var descriptor = Load<OpenPath>(library, "open")(path, DirectoryFlags);
            if (descriptor < 0) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "cannot open process identity"); }
            var handle = new SafeFileHandle(descriptor, ownsHandle: true);
            try { return new LinuxProcessIdentity(library, handle); }
            catch { handle.Dispose(); throw; }
        }
        catch { NativeLibrary.Free(library); throw; }
    }

    internal static void Probe()
    {
        using var self = OpenSelf();
        if (!self.Send(0)) { throw new InvalidOperationException("safe Linux signalling cannot observe the current process"); }
    }

    internal ulong Birth() => Stat().Birth;

    internal bool IsLive()
    {
        try { return Stat().State is not ("Z" or "X" or "x"); }
        catch (Win32Exception problem) when (problem.NativeErrorCode == NoSuchProcess) { return false; }
    }

    internal void RequireChannel(string channel)
    {
        var values = Encoding.UTF8.GetString(Read("cmdline")).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var matches = 0;
        for (var i = 0; i + 1 < values.Length; i++)
        {
            if (values[i] == "--channel" && values[i + 1] == channel) { matches++; }
        }
        if (matches != 1) { throw new InvalidOperationException("bootstrap channel identity mismatch"); }
        _ = Birth();
        if (!IsLive()) { throw new InvalidOperationException("bootstrap identity is no longer live"); }
    }

    internal bool Send(int signal)
    {
        var result = _signal(PidfdSendSignal, _directory.DangerousGetHandle().ToInt32(), signal, 0, 0);
        if (result == 0) { return true; }
        var error = Marshal.GetLastPInvokeError();
        return error == NoSuchProcess ? false : throw new Win32Exception(error, "safe Linux signalling failed");
    }

    public void Dispose()
    {
        _directory.Dispose();
        NativeLibrary.Free(_library);
    }

    private (string State, ulong Birth) Stat()
    {
        var stat = Encoding.UTF8.GetString(Read("stat"));
        var delimiter = stat.LastIndexOf(')');
        if (delimiter < 0) { throw new InvalidDataException("malformed process stat"); }
        var fields = stat[(delimiter + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length <= 19 || !ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var birth)
            ? throw new InvalidDataException("malformed process birth identity") : (fields[0], birth);
    }

    private byte[] Read(string name)
    {
        var descriptor = _openAt(_directory.DangerousGetHandle().ToInt32(), name, CloseOnExec);
        if (descriptor < 0) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "cannot observe process " + name); }
        using var handle = new SafeFileHandle(descriptor, ownsHandle: true);
        using var input = new FileStream(handle, FileAccess.Read);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static T Load<T>(nint library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
}
