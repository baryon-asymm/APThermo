namespace APThermo.Samples;

/// <summary>The samples' tree contract: the docs tests node runs every scenario in-process through this entry point.</summary>
internal static class Program
{
    private static readonly (string Name, Action<TextWriter> Run)[] Table =
    [
        ("quick-start", QuickStart.Run),
        ("rocket", RocketSolve.Run),
        ("rocket-exits", RocketExits.Run),
        ("custom-propellant", CustomPropellant.Run),
        ("equilibrium-kinds", EquilibriumKinds.Run),
        ("batch", BatchSolve.Run),
        ("ratio-sweep", RatioSweep.Run),
        ("states", StatesSolve.Run),
        ("rocket-states", RocketStates.Run),
        ("accelerator-choice", AcceleratorChoice.Run),
        ("database", DatabaseFromFiles.Run),
        ("failures", Failures.Run),
    ];

    public static IReadOnlyList<string> Scenarios { get; } = Table.Select(entry => entry.Name).ToList();

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var entry = args.Length == 1 ? Table.FirstOrDefault(t => t.Name == args[0]) : default;
        if (entry.Run is not null)
        {
            entry.Run(output);
            return 0;
        }

        error.WriteLine("usage: APThermo.Samples <scenario>");
        error.WriteLine("scenarios: " + string.Join(", ", Scenarios));
        return 2;
    }

    /// <summary>The source class name of a scenario, so the docs tests node's L1 can find its snippet regions without a second, hand-written list.</summary>
    internal static string ClassNameOf(string scenario) =>
        Table.First(entry => entry.Name == scenario).Run.Method.DeclaringType!.Name;
}
