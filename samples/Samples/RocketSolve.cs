using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;

namespace APThermo.Samples;

// <!-- snippet: RocketSolve -->
internal sealed class RocketSolve
{
    internal static void Run(TextWriter output)
    {
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();

        var problem = new RocketProblem
        {
            ChamberPressure = 7.0e6,
            AreaRatios = [20.0, 77.5],
            Flow = FlowModel.ShiftingEquilibrium,
            Transport = true,
        };

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        RocketResult result = solver.Solve(propellant, problem);

        output.WriteLine($"LOX/LH2 O/F=6.0  Pc={result.Problem.ChamberPressure / 1e6:F1} MPa");
        foreach (Station station in result.Stations)
        {
            double T = station.State.Temperature;
            double P_MPa = station.State.Pressure / 1e6;
            double Isp = station.Performance?.SpecificImpulse ?? 0;
            output.WriteLine($"  {station.Name,-8}  T={T:F1} K   P={P_MPa:F3} MPa   Isp={Isp:F1} m/s");
        }
    }
}
