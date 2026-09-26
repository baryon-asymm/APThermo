using System.Reflection;
using APThermo.Cli.Syntax;
using APThermo.Execution;

namespace APThermo.Cli;

/// <summary>The entry point of apthermo: arguments in, documents and messages out, an exit code back.</summary>
internal static class Program
{
    public const string ToolName = "apthermo";

    /// <summary>The assembly's informational version without a build suffix.</summary>
    public static string Version { get; } =
        (typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>
    /// Installs the process-wide unhandled-exception handler before running (root BOOT.md, Diagnostics, 2026-09-24):
    /// CA1031 forbids <see cref="Run"/> from catching every exception itself, so whatever leaves it uncaught is
    /// reported here, the same way as a caught one (<see cref="Failures.Unhandled"/>), and ends the process with
    /// exit code 3.
    /// </summary>
    public static int Main(string[] args)
    {
        var error = Console.Error;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Environment.Exit((int)Failures.Unhandled((Exception)e.ExceptionObject, error));
        return Run(args, Console.Out, error);
    }

    /// <summary>
    /// Runs one invocation in-process: documents to <paramref name="output"/> or the file of --output, messages to
    /// <paramref name="error"/>. Catches only the exceptions this node has a documented exit code for (API.md, the
    /// ⚠ of 2026-09-24); every other exception leaves this method, so an in-process caller such as a test receives
    /// it directly, and a real process reports it through <see cref="Main"/>'s unhandled-exception handler instead.
    /// </summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            var invocation = CommandLine.Parse(args);
            if (invocation.Version)
            {
                output.WriteLine(Version);
                return (int)ExitCode.Ok;
            }

            if (invocation.Help)
            {
                output.Write(CommandTable.Usage);
                return (int)ExitCode.Ok;
            }

            return (int)CommandRegistry.Execute(invocation, output);
        }
        catch (InputException e)
        {
            return (int)Failures.Handle(e, error);
        }
        catch (AcceleratorUnavailableException e)
        {
            return (int)Failures.Handle(e, error);
        }
        catch (IOException e)
        {
            return (int)Failures.Unhandled(e, error);
        }
        catch (UnauthorizedAccessException e)
        {
            return (int)Failures.Unhandled(e, error);
        }
    }
}
