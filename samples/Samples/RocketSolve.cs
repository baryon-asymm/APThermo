// snippet-start: RocketSolveUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>A rocket case with transport: the stations, their statuses, the performance figures and one transport figure.</summary>
internal sealed class RocketSolve
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: RocketSolve
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)   // K
            .Fuel("H2(L)", temperature: 20.27)        // K
            .OxidizerToFuelRatio(6.0)
            .Build();

        var problem = new RocketProblem
        {
            ChamberPressure = 7.0e6,                  // Pa
            AreaRatios = [20.0, 77.5],
            Flow = FlowModel.ShiftingEquilibrium,
            Transport = true,
        };

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var result = solver.Solve(propellant, problem);

        void PrintStation(Station station)
        {
            if (station.Status != CaseStatus.Ok)
            {
                output.WriteLine($"  {station.Name,-8}  FAILED: {station.Status}");
                return;
            }

            var temperature = station.State.Temperature;
            var pressure = station.State.Pressure / 1e6;
            output.WriteLine(station.Name == "chamber"
                ? $"  {station.Name,-8}  T={temperature:F1} K   P={pressure:F3} MPa"
                : $"  {station.Name,-8}  T={temperature:F1} K   P={pressure:F3} MPa   Isp={station.Performance!.Value.SpecificImpulse:F1} m/s");
        }

        output.WriteLine($"LOX/LH2 O/F=6.0  Pc={problem.ChamberPressure / 1e6:F1} MPa  status={result.Status}");
        foreach (var station in result.Stations)
        {
            PrintStation(station);
        }

        var throat = result.Stations[1];
        if (throat.Status == CaseStatus.Ok)
        {
            if (throat.Transport is { } transport)
            {
                output.WriteLine($"  throat viscosity = {transport.Viscosity:E3} Pa·s");
            }

            foreach (var (species, fraction) in throat.MoleFractions.OrderByDescending(kv => kv.Value).Take(3))
            {
                output.WriteLine($"  {species,-6} x={fraction:F4}");
            }
        }
        // snippet-end
    }
}
