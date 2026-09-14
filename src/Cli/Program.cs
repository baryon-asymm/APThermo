using System.Reflection;

namespace AerospacePropellantThermodynamics.Cli;

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

            return (int)CommandRegistry.Execute(invocation, output);
        }
        catch (Exception e)
        {
            return (int)Failures.Handle(e, error);
        }
    }
}
