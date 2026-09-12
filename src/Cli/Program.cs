using System.Reflection;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Exit codes of the command line (BOOT.md, invariants).</summary>
public enum ExitCode
{
    /// <summary>Every case and station is Ok.</summary>
    Ok = 0,

    /// <summary>At least one case failed numerically; the document was still written.</summary>
    CaseFailed = 1,

    /// <summary>An invalid input document, option, database path or reactant; nothing was written.</summary>
    InvalidInput = 2,

    /// <summary>An accelerator or infrastructure error.</summary>
    Infrastructure = 3,
}

/// <summary>The entry point of apthermo: arguments in, documents and messages out, an exit code back.</summary>
public static class Program
{
    public const string ToolName = "apthermo";

    /// <summary>The assembly's informational version without a build suffix.</summary>
    public static string Version { get; } =
        (typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>Runs one invocation in-process: documents to <paramref name="output"/> or the file of --output, messages to <paramref name="error"/>.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            var invocation = CommandLine.Parse(args);
            if (invocation.Help)
            {
                output.Write(CommandLine.Usage);
                return (int)ExitCode.Ok;
            }

            return (int)Commands.Execute(invocation, output);
        }
        catch (InputException e)
        {
            error.WriteLine(e.Message);
            return (int)ExitCode.InvalidInput;
        }
        catch (Exception e) when (e is ArgumentException or KeyNotFoundException)
        {
            // The library's rejections by name: an unknown reactant, a temperature out of range, a record without a target.
            error.WriteLine(e.Message);
            return (int)ExitCode.InvalidInput;
        }
        catch (DatabaseFormatException e)
        {
            error.WriteLine($"{e.FileName ?? "database"}:{e.LineNumber}: {e.Message}");
            return (int)ExitCode.InvalidInput;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            error.WriteLine(e.Message);
            return (int)ExitCode.InvalidInput;
        }
        catch (AcceleratorUnavailableException e)
        {
            error.WriteLine(e.Message);
            foreach (var path in e.PathsTried)
            {
                error.WriteLine($"  tried {path}");
            }

            return (int)ExitCode.Infrastructure;
        }
        catch (Exception e)
        {
            error.WriteLine($"{e.GetType().Name}: {e.Message}");
            return (int)ExitCode.Infrastructure;
        }
    }
}

/// <summary>What the user gave cannot be used: an option, a document, a path; exit code 2.</summary>
internal sealed class InputException(string message) : Exception(message);
