using APThermo.Cli.Listings;
using APThermo.Cli.Syntax;

namespace APThermo.Cli;

/// <summary>Command name to handler, no logic (it was the class `Commands`).</summary>
internal static class CommandRegistry
{
    private static readonly IReadOnlyDictionary<string, Func<Invocation, TextWriter, ExitCode>> Handlers =
        new Dictionary<string, Func<Invocation, TextWriter, ExitCode>>(StringComparer.Ordinal)
        {
            ["rocket"] = ProblemCommand.Execute,
            ["equilibrium"] = ProblemCommand.Execute,
            ["states"] = StatesCommand.Execute,
            ["species"] = SpeciesCommand.Execute,
            ["devices"] = DeviceListing.Execute,
            ["schema"] = SchemaCommand.Execute,
        };

    public static ExitCode Execute(Invocation invocation, TextWriter output) =>
        Handlers.TryGetValue(invocation.Command, out var handler) ? handler(invocation, output) : throw new InputException($"unknown command '{invocation.Command}'");
}
