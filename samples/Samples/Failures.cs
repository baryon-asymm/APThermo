// snippet-start: FailuresUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
// snippet-end

namespace APThermo.Samples;

/// <summary>The refusals a consumer must be ready for: the statuses are values, and four situations throw.</summary>
internal sealed class Failures
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: Failures
        var database = SpeciesDatabase.LoadBundled();
        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

        try
        {
            _ = Propellant.From(database).Oxidizer("NOT-A-REACTANT").Fuel("H2(L)").Build();
        }
        catch (KeyNotFoundException exception)
        {
            output.WriteLine($"unknown reactant: {exception.Message}");
        }

        try
        {
            _ = Propellant.From(database)
                .Oxidizer("O2(L)")
                .Fuel("H2(L)")
                .Named("AL(cr)", massFraction: 0.1)
                .OxidizerToFuelRatio(6.0)
                .Build();
        }
        catch (ArgumentException exception)
        {
            output.WriteLine($"named reactant with a ratio: {exception.Message}");
        }

        var composition = new Dictionary<string, double> { ["H"] = 141.73179242528607, ["O"] = 53.57343757533765 };   // ~1 kg
        try
        {
            var record = new StateRecord(Pressure: 1.0e6, Composition: composition, Temperature: 3000.0, Enthalpy: -1.0e6);
            _ = solver.SolveStates([record]);
        }
        catch (StateRecordException exception)
        {
            output.WriteLine($"state record refused: {exception.Reason}");
        }

        var doubled = composition.ToDictionary(kv => kv.Key, kv => 2.0 * kv.Value);   // ~2 kg: too heavy
        try
        {
            var mixture = ElementalMixture.Create(doubled, enthalpy: -1.0e6);
            _ = solver.Solve(mixture, new RocketProblem { ChamberPressure = 1.0e6, AreaRatios = [10.0] });
        }
        catch (MixtureMassException exception)
        {
            output.WriteLine($"mixture too heavy: {exception.Reason}");
        }

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();
        var result = solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, PressureRatios = [0.5] });
        var exit = result.Stations[^1];
        output.WriteLine($"a failed station carries a status instead of a partial state: {exit.Status}");
        // snippet-end
    }
}
