// snippet-start: EquilibriumKindsUsings
using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Problems;
using APThermo.Thermo;
// snippet-end

namespace APThermo.Samples;

/// <summary>The three equilibrium kinds at one pressure: hp at the propellant's own enthalpy, sp at that state's entropy, tp at a fixed temperature.</summary>
internal sealed class EquilibriumKinds
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: EquilibriumKinds
        var database = SpeciesDatabase.LoadBundled();

        var propellant = Propellant.From(database)
            .Oxidizer("O2(L)", temperature: 90.17)   // K
            .Fuel("H2(L)", temperature: 20.27)        // K
            .OxidizerToFuelRatio(6.0)
            .Build();

        using var solver = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

        void Print(string kind, EquilibriumResult result)
        {
            if (result.Status != CaseStatus.Ok)
            {
                output.WriteLine($"  {kind,-2}  FAILED: {result.Status}");
                return;
            }

            output.WriteLine($"  {kind,-2}  T={result.State.State.Temperature:F1} K");
        }

        var hp = solver.Solve(propellant, new EquilibriumProblem { Pressure = 7.0e6 });   // Kind defaults to hp
        Print("hp", hp);

        var sp = solver.Solve(propellant, new EquilibriumProblem
        {
            Kind = ProblemKind.AssignedEntropyPressure,
            Pressure = 7.0e6,
            Entropy = hp.State.State.Entropy,
        });
        Print("sp", sp);

        var tp = solver.Solve(propellant, new EquilibriumProblem
        {
            Kind = ProblemKind.AssignedTemperaturePressure,
            Pressure = 7.0e6,
            Temperature = 3000.0,   // K
        });
        Print("tp", tp);
        // snippet-end
    }
}
