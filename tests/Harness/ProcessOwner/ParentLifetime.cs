using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace APThermo.Harness.ProcessOwner;

/// <summary>Retains and observes one validated parent identity before channel connection.</summary>
internal sealed class ParentLifetime : IDisposable
{
    private readonly Process? _windows;
    private readonly LinuxProcessIdentity? _linux;
    private readonly nint _library;
    private readonly Wait? _wait;

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate uint Wait(nint handle, uint milliseconds);

    private ParentLifetime(Process windows, nint library)
    {
        _windows = windows;
        _library = library;
        _wait = Marshal.GetDelegateForFunctionPointer<Wait>(NativeLibrary.GetExport(library, "WaitForSingleObject"));
    }

    private ParentLifetime(LinuxProcessIdentity linux)
    {
        _linux = linux;
    }

    internal static ulong SelfBirth()
    {
        if (OperatingSystem.IsLinux())
        {
            using var current = LinuxProcessIdentity.OpenSelf();
            return current.Birth();
        }
        using var process = Process.GetCurrentProcess();
        return checked((ulong)process.StartTime.ToUniversalTime().ToFileTimeUtc());
    }

    internal static ParentLifetime Open(int pid, ulong birth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pid);
        if (OperatingSystem.IsLinux()) { return OpenLinux(pid, birth); }
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        var process = Process.GetProcessById(pid);
        nint library = 0;
        try
        {
            _ = process.SafeHandle;
            if (process.HasExited || checked((ulong)process.StartTime.ToUniversalTime().ToFileTimeUtc()) != birth)
            {
                throw new InvalidOperationException("parent process identity mismatch or exit");
            }
            library = NativeLibrary.Load("kernel32.dll");
            var result = new ParentLifetime(process, library);
            return result.IsLive() ? result : throw new InvalidOperationException("parent process already exited");
        }
        catch
        {
            if (library != 0) { NativeLibrary.Free(library); }
            process.Dispose();
            throw;
        }
    }

    private static ParentLifetime OpenLinux(int pid, ulong birth)
    {
        var identity = LinuxProcessIdentity.Open(pid);
        try
        {
            return identity.Birth() == birth && identity.IsLive()
                ? new ParentLifetime(identity) : throw new InvalidOperationException("parent process identity mismatch or exit");
        }
        catch { identity.Dispose(); throw; }
    }

    internal bool IsLive()
    {
        if (_linux is not null) { return _linux.IsLive(); }
        var result = _wait!(_windows!.SafeHandle.DangerousGetHandle(), 0);
        return result switch
        {
            0 => false,
            258 => true,
            _ => throw new Win32Exception(Marshal.GetLastPInvokeError(), "parent lifetime observation failed"),
        };
    }

    internal async Task WaitForExitAsync(CancellationToken token)
    {
        while (IsLive()) { await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false); }
    }

    public void Dispose()
    {
        _linux?.Dispose();
        _windows?.Dispose();
        if (_library != 0) { NativeLibrary.Free(_library); }
    }
}
