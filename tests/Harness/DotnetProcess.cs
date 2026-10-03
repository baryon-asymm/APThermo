using System.Diagnostics;
using APThermo.Fixtures;

namespace APThermo.Harness;

/// <summary>What one run of <see cref="DotnetProcess.Run"/> left behind: the exit code and the raw bytes of both standard streams.</summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="Output">The bytes the process wrote to standard output, exactly as written, in no encoding.</param>
/// <param name="Error">The bytes the process wrote to standard error, exactly as written, in no encoding.</param>
public sealed record DotnetProcessResult(int ExitCode, ReadOnlyMemory<byte> Output, ReadOnlyMemory<byte> Error);

/// <summary>
/// Runs a built .NET assembly as a separate process through the <c>dotnet</c> host, so that exit codes, environment
/// variables and the standard streams are real. It names no type of the node under test: the assembly is a path, the
/// arguments strings. The streams are read as bytes from the process's base streams, never decoded, so a caller sees
/// the encoding the process wrote on the platform it ran on.
/// </summary>
public static class DotnetProcess
{
    private const string Host = "dotnet";

    /// <summary>
    /// Starts <c>dotnet &lt;assemblyPath&gt; &lt;arguments&gt;</c> in <paramref name="workingDirectory"/>, with
    /// <paramref name="environment"/> set on top of the inherited environment, and waits for it to end.
    /// </summary>
    /// <param name="assemblyPath">The path of the assembly to run, as <see cref="BuiltAssemblyPath"/> finds it.</param>
    /// <param name="arguments">The arguments, each passed as one argument of the process, never re-parsed.</param>
    /// <param name="workingDirectory">The directory the process starts in.</param>
    /// <param name="environment">Variables to set for this process only, or none.</param>
    /// <returns>The exit code and the raw bytes of standard output and standard error.</returns>
    /// <exception cref="InvalidOperationException">The <c>dotnet</c> host did not start.</exception>
    public static DotnetProcessResult Run(
        string assemblyPath, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        var start = new ProcessStartInfo(Host)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        start.ArgumentList.Add(assemblyPath);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start");
        var output = ReadAllBytes(process.StandardOutput.BaseStream);
        var error = ReadAllBytes(process.StandardError.BaseStream);
        process.WaitForExit();
        return new DotnetProcessResult(process.ExitCode, output.Result, error.Result);
    }

    /// <summary>
    /// The path of a built assembly of the tree: the one next to the running test assembly when its runtime
    /// configuration was copied there, else the build output of the project in the running configuration.
    /// </summary>
    /// <param name="assemblyName">The assembly's name without the extension.</param>
    /// <param name="projectDirectory">The project's directory below the repository root, one segment per argument.</param>
    /// <returns>The path of the <c>.dll</c>.</returns>
    /// <exception cref="FileNotFoundException">The assembly was not built.</exception>
    public static string BuiltAssemblyPath(string assemblyName, params string[] projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        ArgumentNullException.ThrowIfNull(projectDirectory);
        var local = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
        if (File.Exists(local) && File.Exists(Path.Combine(AppContext.BaseDirectory, assemblyName + ".runtimeconfig.json")))
        {
            return local;
        }

        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ? "Release" : "Debug";
        var own = RepositoryPaths.Resolve([.. projectDirectory, "bin", configuration, "net10.0", assemblyName + ".dll"]);
        return File.Exists(own) ? own : throw new FileNotFoundException($"the assembly {assemblyName} was not built", own);
    }

    private static async Task<byte[]> ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
