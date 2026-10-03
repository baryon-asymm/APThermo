using System.Text;
using APThermo.Cli;
using APThermo.Harness;

namespace APThermo.Docs.Tests;

/// <summary>The result of one invocation of the command line: exit code, standard output and standard error as text.</summary>
internal sealed record CliRun(int Code, string Output, string Error);

/// <summary>
/// The two ways this node runs the command line (BOOT.md, L3): in-process through <c>Program.RunCached</c>, on warm
/// solvers shared by every class of this node, and as a fresh process, the shipped cold path, whose standard streams are
/// read as bytes and decoded as UTF-8. Classes of this node run in parallel; the cache's gate serializes the in-process
/// invocations, and a process shares nothing.
/// </summary>
internal static class CliInvocation
{
    private static readonly SolverCache Solvers = NewSolvers();

    /// <summary>Runs <paramref name="args"/> in-process on the warm solvers of this node.</summary>
    public static CliRun InProcess(IReadOnlyList<string> args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Program.RunCached([.. args], output, error, Solvers);
        return new CliRun(code, output.ToString(), error.ToString());
    }

    /// <summary>Runs <paramref name="args"/> as <c>dotnet APThermo.Cli.dll</c>, a fresh process in the temporary directory.</summary>
    public static CliRun AsProcess(IReadOnlyList<string> args)
    {
        var result = DotnetProcess.Run(DotnetProcess.BuiltAssemblyPath("APThermo.Cli", "src", "Cli"), args, Path.GetTempPath());
        return new CliRun(result.ExitCode, Encoding.UTF8.GetString(result.Output.Span), Encoding.UTF8.GetString(result.Error.Span));
    }

    private static SolverCache NewSolvers()
    {
        var solvers = new SolverCache();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => solvers.Dispose();
        return solvers;
    }
}
