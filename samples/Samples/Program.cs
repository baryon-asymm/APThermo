namespace APThermo.Samples;

internal static class Program
{
    public static IReadOnlyList<string> Scenarios { get; } = new[]
    {
        "rocket", "batch", "equilibrium", "states"
    };

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 1 || Scenarios.Contains(args[0]) is false)
        {
            error.WriteLine("usage: APThermo.Samples <scenario>");
            error.WriteLine("scenarios: " + string.Join(", ", Scenarios));
            return 2;
        }

        switch (args[0])
        {
            case "rocket": RocketSolve.Run(output); return 0;
            case "batch": BatchSolve.Run(output); return 0;
            case "equilibrium": EquilibriumSolve.Run(output); return 0;
            case "states": StatesSolve.Run(output); return 0;
            default: return 2;
        }
    }
}
