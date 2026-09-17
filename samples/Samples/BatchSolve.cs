using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;

namespace APThermo.Samples;

// <!-- snippet: BatchSolve -->
internal sealed class BatchSolve
{
    internal static void Run(TextWriter output)
    {
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();

        double[] pressures = [5.0e6, 7.0e6, 10.0e6];
        var problems = pressures.Select(pc => new RocketProblem
        {
            ChamberPressure = pc,
            AreaRatios = [20.0, 77.5],
            Flow = FlowModel.ShiftingEquilibrium,
            Transport = false,
        }).ToList();

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        IReadOnlyList<RocketResult> results = solver.Solve(propellant, problems);

        output.WriteLine("LOX/LH2 O/F=6.0  area ratios [20, 77.5]");
        foreach (RocketResult result in results)
        {
            Station exit = result.Stations[^1];
            double Pc_MPa = result.Problem.ChamberPressure / 1e6;
            double Isp = exit.Performance?.SpecificImpulse ?? 0;
            output.WriteLine($"  Pc={Pc_MPa:F1} MPa   Isp_exit={Isp:F1} m/s");
        }
    }
}
