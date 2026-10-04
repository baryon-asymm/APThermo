using APThermo.Fixtures;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The tp states of the trace-gas families that the pass does not settle and that are declared so (TraceGas BOOT.md, "Declared
/// leftovers"): one <c>kind name</c> pair per line in <c>TraceGasLeftovers.txt</c>, the names those of
/// <see cref="TraceGasCase.Name"/>. Kind <c>notconverged</c>: the state ends <c>NotConverged</c>, as before the pass. Kind
/// <c>residue</c>: the state ends <c>Ok</c> with less than 1e-12 kmol/kg of gas, through the trace-gas pass after a verdict that
/// did not prove it gasless (the face search of the verdict found no certificate), the one residue-gas answer the design expected the
/// verdict to turn into <c>NoGasPhase</c>. A state not in the list that fails is a regression; a state in the list that is
/// settled is a stale declaration, and the scan fact says so.
/// </summary>
internal static class TraceGasLeftovers
{
    /// <summary>The path of the list.</summary>
    public static string Path => RepositoryPaths.Resolve("tests", "Equilibrium.Tests", "TraceGasLeftovers.txt");

    /// <summary>The names declared <c>NotConverged</c>.</summary>
    public static IReadOnlySet<string> NotConverged { get; } = Load("notconverged");

    /// <summary>The names declared <c>Ok</c> with a residue of gas.</summary>
    public static IReadOnlySet<string> Residue { get; } = Load("residue");

    /// <summary>Whether the state is declared <c>NotConverged</c>.</summary>
    public static bool Declared(string name) => NotConverged.Contains(name);

    private static HashSet<string> Load(string kind) =>
        [.. File.ReadAllLines(Path).Select(line => line.Trim()).Where(line => line.StartsWith(kind + " ", StringComparison.Ordinal)).Select(line => line[(kind.Length + 1)..])];
}
