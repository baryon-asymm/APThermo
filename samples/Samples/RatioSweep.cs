// snippet-start: RatioSweepUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>An oxidizer-to-fuel sweep as one batch: a mixture per ratio, then the mixtures overload of <c>Solve</c>.</summary>
internal sealed class RatioSweep
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: RatioSweep
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)   // K
            .Fuel("H2(L)", temperature: 20.27)        // K
            .OxidizerToFuelRatio(6.0)
            .Build();

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

        double[] ratios = [5.0, 6.0, 7.0];
        var mixtures = ratios.Select(ratio => solver.MixtureOf(propellant, ratio)).ToList();
        var problems = ratios.Select(_ => new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] }).ToList();

        IReadOnlyList<RocketResult> results = solver.Solve(mixtures, problems);

        output.WriteLine("LOX/LH2 O/F sweep, one batch, Pc=7.0 MPa");
        for (var i = 0; i < ratios.Length; i++)
        {
            Station exit = results[i].Stations[^1];
            if (exit.Status != CaseStatus.Ok)
            {
                output.WriteLine($"  O/F={ratios[i]:F1}  FAILED: {exit.Status}");
                continue;
            }

            output.WriteLine($"  O/F={ratios[i]:F1}  Isp_exit={exit.Performance!.Value.SpecificImpulse:F1} m/s");
        }
        // snippet-end
    }
}
