using System.Diagnostics;
using APThermo.Fixtures;

namespace APThermo.Harness;

/// <summary>What one run of <see cref="DotnetProcess"/> left behind: the exit code and the raw bytes of both standard streams.</summary>
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

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Runs an assembly with a five-minute process/stream deadline.</summary>
    /// <param name="assemblyPath">The built assembly path.</param>
    /// <param name="arguments">Arguments passed one to one without a shell.</param>
    /// <param name="workingDirectory">The process working directory.</param>
    /// <param name="environment">Environment variables to add or replace.</param>
    /// <returns>Exit code and exact raw bytes from both streams.</returns>
    public static DotnetProcessResult Run(string assemblyPath, IReadOnlyList<string> arguments, string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null) =>
        Run(assemblyPath, arguments, workingDirectory, DefaultTimeout, environment);

    /// <summary>Runs an assembly with a shared process/stream deadline and caller cancellation.</summary>
    /// <param name="assemblyPath">The built assembly path.</param>
    /// <param name="arguments">Arguments passed one to one without a shell.</param>
    /// <param name="workingDirectory">The process working directory.</param>
    /// <param name="timeout">A positive duration representable by the .NET timer.</param>
    /// <param name="environment">Environment variables to add or replace.</param>
    /// <param name="cancellationToken">Caller cancellation; a pre-cancelled token prevents launch.</param>
    /// <returns>Exit code and exact raw bytes from both streams.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The timeout is nonpositive or exceeds the timer range.</exception>
    /// <exception cref="TimeoutException">The process or its streams exceeded the deadline.</exception>
    /// <exception cref="OperationCanceledException">Caller cancellation interrupted the process or streams.</exception>
    /// <exception cref="InvalidOperationException">Process.Start returned no process.</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">The operating system could not start the dotnet host.</exception>
    public static DotnetProcessResult Run(string assemblyPath, IReadOnlyList<string> arguments, string workingDirectory,
        TimeSpan timeout, IReadOnlyDictionary<string, string>? environment = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "expected a positive .NET timer-representable duration");
        }
        if (cancellationToken.IsCancellationRequested)
        {
            var problem = new OperationCanceledException("dotnet process was cancelled before launch; stdout=0 bytes, stderr=0 bytes", cancellationToken);
            problem.Data["StandardOutput"] = Array.Empty<byte>();
            problem.Data["StandardError"] = Array.Empty<byte>();
            throw problem;
        }
        return RunAsync(assemblyPath, arguments, workingDirectory, timeout, environment, cancellationToken).GetAwaiter().GetResult();
    }

    private static async Task<DotnetProcessResult> RunAsync(string assemblyPath, IReadOnlyList<string> arguments, string workingDirectory,
        TimeSpan timeout, IReadOnlyDictionary<string, string>? environment, CancellationToken cancellationToken)
    {
        var start = StartInfo(assemblyPath, arguments, workingDirectory, environment);
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start");
        using var output = new ProcessOutput(process.StandardOutput.BaseStream);
        using var error = new ProcessOutput(process.StandardError.BaseStream);
        return await CompleteAsync(process, output, error, linked.Token, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessStartInfo StartInfo(string assemblyPath, IReadOnlyList<string> arguments, string workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
    {
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
        return start;
    }

    private static async Task<DotnetProcessResult> CompleteAsync(Process process, ProcessOutput output, ProcessOutput error,
        CancellationToken linkedToken, CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(), output.Completion, error.Completion).WaitAsync(linkedToken).ConfigureAwait(false);
            return new DotnetProcessResult(process.ExitCode, output.Snapshot(), error.Snapshot());
        }
        catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
        {
            var cleanup = await CleanupAsync(process, output, error).ConfigureAwait(false);
            var capturedOutput = output.Snapshot();
            var capturedError = error.Snapshot();
            var reason = cancellationToken.IsCancellationRequested ? "was cancelled" : "exceeded its deadline";
            var message = $"dotnet process {process.Id} {reason}; {cleanup}. " +
                $"stdout={capturedOutput.Length} bytes, stderr={capturedError.Length} bytes.\n" +
                $"stdout: {Excerpt(capturedOutput)}\nstderr: {Excerpt(capturedError)}";
            Exception problem = cancellationToken.IsCancellationRequested
                ? new OperationCanceledException(message, cancellationToken)
                : new TimeoutException(message);
            problem.Data["StandardOutput"] = capturedOutput;
            problem.Data["StandardError"] = capturedError;
            throw problem;
        }
    }

    private static async Task<string> CleanupAsync(Process process, ProcessOutput output, ProcessOutput error)
    {
        using var cancellation = new CancellationTokenSource(CleanupTimeout);
        var kill = Task.Run(() => AttemptTreeKill(process));
        try
        {
            await Task.WhenAll(kill, process.WaitForExitAsync(cancellation.Token), output.Completion, error.Completion)
                .WaitAsync(cancellation.Token).ConfigureAwait(false);
            return await kill.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return "tree kill attempted; cleanup deadline reached";
        }
        catch (Exception problem) when (problem is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "tree kill attempted; cleanup: " + problem.GetType().Name;
        }
        finally
        {
            output.Stop();
            error.Stop();
        }
    }

    private static string AttemptTreeKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            return "tree kill attempted; streams drained";
        }
        catch (Exception problem) when (problem is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "tree kill attempted: " + problem.GetType().Name;
        }
    }

    private static string Excerpt(byte[] bytes) =>
        System.Text.Json.JsonSerializer.Serialize(System.Text.Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 256))));

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

}
