using APThermo.Data;
using APThermo.Execution;
using APThermo.Problems;

namespace APThermo.Samples;

// <!-- snippet: EquilibriumSolve -->
internal sealed class EquilibriumSolve
{
    internal static void Run(TextWriter output)
    {
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)
            .Fuel("H2(L)", temperature: 20.27)
            .OxidizerToFuelRatio(6.0)
            .Build();

        var problem = new EquilibriumProblem { Pressure = 7.0e6 };

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        EquilibriumResult result = solver.Solve(propellant, problem);

        var state = result.State;
        output.WriteLine($"LOX/LH2 O/F=6.0  hp at P={result.Problem.Pressure / 1e6:F1} MPa");
        output.WriteLine($"  T       = {state.State.Temperature:F1} K");
        output.WriteLine($"  P       = {state.State.Pressure / 1e6:F3} MPa");
        output.WriteLine($"  h       = {state.State.Enthalpy / 1e3:F1} kJ/kg");
        output.WriteLine($"  rho     = {state.State.Density:F3} kg/m³");
        output.WriteLine($"  M       = {state.State.MolarMass:F2} kg/kmol");
    }
}
