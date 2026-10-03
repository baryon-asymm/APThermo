using APThermo.Cli.Listings;
using APThermo.Cli.Syntax;
using APThermo.Execution;

namespace APThermo.Cli;

/// <summary>Command name to handler, no logic (it was the class `Commands`).</summary>
internal static class CommandRegistry
{
    private static readonly Dictionary<string, Func<Invocation, TextWriter, Func<string?, AcceleratorKind, SolverSession>, ExitCode>> Handlers =
        new(StringComparer.Ordinal)
        {
            ["rocket"] = ProblemCommand.Execute,
            ["equilibrium"] = ProblemCommand.Execute,
            ["states"] = StatesCommand.Execute,
            ["species"] = (invocation, output, _) => SpeciesCommand.Execute(invocation, output),
            ["devices"] = (invocation, output, _) => DeviceListing.Execute(invocation, output),
            ["schema"] = (invocation, output, _) => SchemaCommand.Execute(invocation, output),
        };

    public static ExitCode Execute(Invocation invocation, TextWriter output, Func<string?, AcceleratorKind, SolverSession> open) =>
        Handlers.TryGetValue(invocation.Command, out var handler) ? handler(invocation, output, open) : throw new InputException($"unknown command '{invocation.Command}'");
}
