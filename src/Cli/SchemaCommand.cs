using APThermo.Cli.Output;
using APThermo.Cli.Syntax;

namespace APThermo.Cli;

/// <summary>The schema command: prints an embedded JSON schema to standard output or a file.</summary>
internal static class SchemaCommand
{
    public static ExitCode Execute(Invocation invocation, TextWriter output)
    {
        if (invocation.Arguments.Count != 1)
        {
            output.WriteLine("usage: apthermo schema <name>");
            return ExitCode.InvalidInput;
        }

        var name = invocation.Arguments[0];
        if (!SchemaResources.TryGet(name, out var text))
        {
            output.WriteLine($"unknown schema '{name}'; expected one of: {string.Join(", ", SchemaResources.Names)}");
            return ExitCode.InvalidInput;
        }

        DocumentWriter.Deliver(text, invocation.Options.Output, output);
        return ExitCode.Ok;
    }
}
