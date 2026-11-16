using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace APThermo.Harness.ProcessOwner;

/// <summary>Bootstrap-only session, stream and group operations.</summary>
internal static class NativeOwner
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    private delegate int NoArguments();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    private delegate int IntegerArgument(int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    private delegate int FileControl(int descriptor, int command);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, SetLastError = true)]
    private delegate int KillGroup(int process, int signal);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate nint StandardHandle(int identifier);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool CloseHandle(nint handle);

    internal static void Establish(string? name)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsJob.Join(name ?? throw new InvalidOperationException("missing job name"));
            return;
        }
        if (!OperatingSystem.IsLinux()) { throw new PlatformNotSupportedException(); }
        var library = NativeLibrary.Load("libc.so.6");
        try
        {
            if (Load<NoArguments>(library, "setsid")() < 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "setsid failed");
            }
        }
        finally { NativeLibrary.Free(library); }
    }

    internal static void CloseOutputs()
    {
        var library = NativeLibrary.Load(OperatingSystem.IsWindows() ? "kernel32.dll" : "libc.so.6");
        try
        {
            if (OperatingSystem.IsWindows())
            {
                CloseWindowsOutputs(library);
            }
            else
            {
                CloseLinuxOutputs(library);
            }
        }
        finally { NativeLibrary.Free(library); }
    }

    private static void CloseWindowsOutputs(nint library)
    {
        var get = Load<StandardHandle>(library, "GetStdHandle");
        var close = Load<CloseHandle>(library, "CloseHandle");
        foreach (var number in new[] { -11, -12 })
        {
            var handle = get(number);
            if (handle is not (0 or -1)) { _ = close(handle); }
        }
    }

    private static void CloseLinuxOutputs(nint library)
    {
        const string descriptors = "/proc/self/fd";
        var control = Load<FileControl>(library, "fcntl");
        var close = Load<IntegerArgument>(library, "close");
        var outputs = new[] { new FileInfo(Path.Combine(descriptors, "1")).LinkTarget,
            new FileInfo(Path.Combine(descriptors, "2")).LinkTarget };
        if (outputs.Any(static output => output is null || !output.StartsWith("pipe:[", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("bootstrap output is not an inherited pipe");
        }
        var duplicates = new List<int>();
        foreach (var path in Directory.EnumerateFiles(descriptors))
        {
            if (!int.TryParse(Path.GetFileName(path), NumberStyles.None, CultureInfo.InvariantCulture, out var descriptor) || descriptor < 3)
            {
                continue;
            }
            string? target;
            try { target = new FileInfo(path).LinkTarget; }
            catch (FileNotFoundException) { continue; }
            if (!outputs.Contains(target, StringComparer.Ordinal)) { continue; }
            var fdFlags = control(descriptor, 1); // F_GETFD
            var statusFlags = control(descriptor, 3); // F_GETFL
            if (fdFlags >= 0 && (fdFlags & 1) != 0 && statusFlags >= 0 && (statusFlags & 3) == 1)
            {
                duplicates.Add(descriptor);
            }
        }
        foreach (var descriptor in duplicates)
        {
            var path = Path.Combine(descriptors, descriptor.ToString(CultureInfo.InvariantCulture));
            string? target;
            try { target = new FileInfo(path).LinkTarget; }
            catch (FileNotFoundException) { continue; }
            if (outputs.Contains(target, StringComparer.Ordinal) && (control(descriptor, 1) & 1) != 0 &&
                (control(descriptor, 3) & 3) == 1 && close(descriptor) != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "close duplicate output failed");
            }
        }
        if (close(1) != 0 || close(2) != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "close output failed");
        }
    }

    internal static void Terminate(string? name)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsJob.StopNamed(name ?? throw new InvalidOperationException("missing job name"));
            return;
        }
        var library = NativeLibrary.Load("libc.so.6");
        try
        {
            if (Load<KillGroup>(library, "kill")(0, 9) != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "group termination failed");
            }
        }
        finally { NativeLibrary.Free(library); }
    }

    private static T Load<T>(nint library, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
}
