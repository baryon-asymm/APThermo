// snippet-start: StatesSolveUsings
using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>State records of another simulation: literal element moles per kilogram, a pressure and one target each.</summary>
internal sealed class StatesSolve
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: StatesSolve
        var database = SpeciesDatabase.LoadBundled();
        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

        var elementMoles = new Dictionary<string, double>
        {
            ["H"] = 141.73179242528607, ["O"] = 53.57343757533765,
        };   // mol/kg, LOX/LH2 at O/F=6.0

        var mixture = ElementalMixture.Create(elementMoles);
        output.WriteLine($"mass = {solver.MassOf(mixture):F4} kg");

        double[] pressures = [1.0e6, 7.0e6];   // Pa
        var records = pressures.Select(pressure => new StateRecord(
            Pressure: pressure,
            Composition: elementMoles,
            Temperature: 3000.0)).ToList();     // K: a tp record

        IReadOnlyList<EquilibriumResult> results = solver.SolveStates(records);

        for (var i = 0; i < pressures.Length; i++)
        {
            EquilibriumResult result = results[i];
            if (result.Status != CaseStatus.Ok)
            {
                output.WriteLine($"  P={pressures[i] / 1e6:F2} MPa  FAILED: {result.Status}");
                continue;
            }

            var state = result.State.State;
            output.WriteLine($"  P={pressures[i] / 1e6:F2} MPa   T={state.Temperature:F1} K   h={state.Enthalpy / 1e3:F1} kJ/kg");
        }
        // snippet-end
    }
}
