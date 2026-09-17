// snippet-start: RocketStatesUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>A state record of another simulation with exits: <c>SolveRocketStates</c> over one record with area ratios.</summary>
internal sealed class RocketStates
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: RocketStates
        var database = SpeciesDatabase.LoadBundled();
        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

        var composition = new Dictionary<string, double>
        {
            ["C"] = 9.505849129331365, ["H"] = 35.214695099119155, ["O"] = 15.704786718374072,
            ["N"] = 6.007718569653603, ["Cl"] = 3.3293409533388547, ["Al"] = 14.709996403305084,
        };   // mol/kg

        var record = new StateRecord(Pressure: 6.5e6, Composition: composition, Enthalpy: -1527829.408385985)
        {
            AreaRatios = [8.0, 12.0],
        };

        IReadOnlyList<RocketResult> results = solver.SolveRocketStates([record]);
        RocketResult result = results[0];

        output.WriteLine($"state record with exits  status={result.Status}  mass={result.MixtureMass:F4} kg");
        foreach (Station station in result.Stations)
        {
            if (station.Status != CaseStatus.Ok)
            {
                output.WriteLine($"  {station.Name,-8}  FAILED: {station.Status}");
                continue;
            }

            var temperature = station.State.Temperature;
            output.WriteLine(station.Name == "chamber"
                ? $"  {station.Name,-8}  T={temperature:F1} K"
                : $"  {station.Name,-8}  T={temperature:F1} K   Isp={station.Performance!.Value.SpecificImpulse:F1} m/s");
        }
        // snippet-end
    }
}
