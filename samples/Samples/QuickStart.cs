// snippet-start: QuickStartUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>The smallest solve: one rocket case at a fixed area ratio, quoted by the package READMEs.</summary>
internal sealed class QuickStart
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: QuickStart
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)   // K
            .Fuel("H2(L)", temperature: 20.27)        // K
            .OxidizerToFuelRatio(6.0)
            .Build();

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var result = solver.Solve(propellant, new RocketProblem
        {
            ChamberPressure = 7.0e6,   // Pa
            AreaRatios = [20.0],
        });

        var chamber = result.Stations[0];
        if (chamber.Status == CaseStatus.Ok)
        {
            output.WriteLine($"chamber temperature = {chamber.State.Temperature:F1} K");
        }
        // snippet-end
    }
}
