// snippet-start: RocketExitsUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>A rocket case mixing pressure-ratio and area-ratio exits, frozen at the throat.</summary>
internal sealed class RocketExits
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: RocketExits
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)   // K
            .Fuel("H2(L)", temperature: 20.27)        // K
            .OxidizerToFuelRatio(6.0)
            .Build();

        var problem = new RocketProblem
        {
            ChamberPressure = 7.0e6,   // Pa
            PressureRatios = [10.0],   // p_c / p_e, reported first
            AreaRatios = [50.0],       // A / A_t, reported after
            Flow = FlowModel.FrozenAtThroat,
        };

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        RocketResult result = solver.Solve(propellant, problem);

        void PrintStation(Station station)
        {
            if (station.Status != CaseStatus.Ok)
            {
                output.WriteLine($"  {station.Name,-8}  FAILED: {station.Status}");
                return;
            }

            var temperature = station.State.Temperature;
            output.WriteLine(station.Name == "chamber"
                ? $"  {station.Name,-8}  T={temperature:F1} K"
                : $"  {station.Name,-8}  T={temperature:F1} K   Isp={station.Performance!.Value.SpecificImpulse:F1} m/s");
        }

        output.WriteLine($"LOX/LH2 O/F=6.0  Pc={problem.ChamberPressure / 1e6:F1} MPa  status={result.Status}");
        foreach (Station station in result.Stations)
        {
            PrintStation(station);
        }
        // snippet-end
    }
}
