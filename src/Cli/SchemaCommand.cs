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
            throw new InputException($"schema needs a name; known schemas: {string.Join(", ", SchemaResources.Names)}");
        }

        var name = invocation.Arguments[0];
        if (!SchemaResources.TryGet(name, out var text))
        {
            throw new InputException($"unknown schema '{name}'; known schemas: {string.Join(", ", SchemaResources.Names)}");
        }

        DocumentWriter.Deliver(text, invocation.Options.Output, output);
        return ExitCode.Ok;
    }
}
