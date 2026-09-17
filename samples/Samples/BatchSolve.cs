// snippet-start: BatchSolveUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>A chamber-pressure sweep as one batch: one propellant, a list of <c>RocketProblem</c>, one <c>Solve</c> call.</summary>
internal sealed class BatchSolve
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: BatchSolve
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)   // K
            .Fuel("H2(L)", temperature: 20.27)        // K
            .OxidizerToFuelRatio(6.0)
            .Build();

        double[] pressures = [5.0e6, 7.0e6, 10.0e6];   // Pa
        var problems = pressures.Select(pressure => new RocketProblem
        {
            ChamberPressure = pressure,
            AreaRatios = [20.0, 77.5],
            Flow = FlowModel.ShiftingEquilibrium,
        }).ToList();

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        IReadOnlyList<RocketResult> results = solver.Solve(propellant, problems);

        output.WriteLine("LOX/LH2 O/F=6.0  area ratios [20, 77.5]");
        foreach (RocketResult result in results)
        {
            Station exit = result.Stations[^1];
            var chamberPressure = result.Problem.ChamberPressure / 1e6;
            if (exit.Status != CaseStatus.Ok)
            {
                output.WriteLine($"  Pc={chamberPressure:F1} MPa  FAILED: {exit.Status}");
                continue;
            }

            output.WriteLine($"  Pc={chamberPressure:F1} MPa   Isp_exit={exit.Performance!.Value.SpecificImpulse:F1} m/s");
        }
        // snippet-end
    }
}
