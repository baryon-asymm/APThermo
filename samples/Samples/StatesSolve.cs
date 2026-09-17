using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;

namespace APThermo.Samples;

// <!-- snippet: StatesSolve -->
internal sealed class StatesSolve
{
    internal static void Run(TextWriter output)
    {
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        ElementalMixture mixture = solver.MixtureOf(propellant);

        double[] pressures = [1.0e5, 7.0e6, 20.0e6];
        var records = pressures.Select(p => new StateRecord(
            Pressure: p,
            Composition: mixture.ElementMoles,
            Temperature: 3000.0
        )).ToList();

        IReadOnlyList<EquilibriumResult> results = solver.SolveStates(records);

        output.WriteLine("LOX/LH2 O/F=6.0  tp states at T=3000 K");
        foreach (EquilibriumResult result in results)
        {
            var s = result.State.State;
            double P_MPa = result.Problem.Pressure / 1e6;
            output.WriteLine($"  P={P_MPa:F2} MPa   T={s.Temperature:F1} K   h={s.Enthalpy / 1e3:F1} kJ/kg   rho={s.Density:F3} kg/m³");
        }
    }
}
